using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Ciribob.FS3D.SimpleRadio.Standalone.Client.Network;
using Ciribob.FS3D.SimpleRadio.Standalone.Client.Settings;
using Ciribob.FS3D.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.FS3D.SimpleRadio.Standalone.Client.Singletons.Models;
using Ciribob.FS3D.SimpleRadio.Standalone.Client.Utils;
using Ciribob.FS3D.SimpleRadio.Standalone.Common.Network.Client;
using Ciribob.FS3D.SimpleRadio.Standalone.Common.Settings;
using Ciribob.FS3D.SimpleRadio.Standalone.Common.Settings.Input;
using Ciribob.FS3D.SimpleRadio.Standalone.Common.Settings.Setting;
using Ciribob.SRS.Common.Network.Client;
using Ciribob.SRS.Common.Network.Models;
using Ciribob.SRS.Common.Network.Singletons;
using NLog;

namespace Ciribob.FS3D.SimpleRadio.Standalone.Client.Audio.Managers;

public class UDPClientAudioProcessor : IDisposable
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly AudioManager _audioManager;
    private readonly ConnectedClientsSingleton _clients = ConnectedClientsSingleton.Instance;
    private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;
    private readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;
    private readonly string _guid;
    private readonly RadioReceivingState[] _radioReceivingState;
    private readonly SyncedServerSettings _serverSettings = SyncedServerSettings.Instance;
    private readonly CancellationTokenSource _stopFlag = new();
    private readonly UDPVoiceHandler _udpClient;
    private readonly object lockObj = new();
    private long _firstPTTPress; // to delay start PTT time
    private long _lastPTTPress; // to handle dodgy PTT - release time

    private ulong _packetNumber = 1;


    private volatile bool _ptt;
    private bool _stop;
    private UDPCommandListener _udpCommandListener;
    private UDPStateSender _udpStateSender;

    public UDPClientAudioProcessor(UDPVoiceHandler udpClient, AudioManager audioManager, string guid)
    {
        _udpClient = udpClient;
        _audioManager = audioManager;
        _guid = guid;

        _radioReceivingState = ClientStateSingleton.Instance.RadioReceivingState;
    }

    public void Dispose()
    {
        _ptt = false;
        _stopFlag?.Dispose();
        _udpCommandListener?.Stop();
    }

    public void Start()
    {
        _ptt = false;
        _packetNumber = 1;
        var decoderThread = new Thread(UdpAudioDecode);
        decoderThread.Start();
        InputDeviceManager.Instance.StartPTTListening(PTTHandler);
        _udpCommandListener = new UDPCommandListener();
        _udpCommandListener.Start();
        _udpStateSender = new UDPStateSender();
        _udpStateSender.Start();
    }


    private SRClientBase IsClientMetaDataValid(string clientGuid)
    {
        if (_clients.ContainsKey(clientGuid))
        {
            var client = _clients[_guid];

            if (client != null) return client;
        }

        return null;
    }


    private void RetransmitAudio(UDPVoicePacket udpVoicePacket,
        List<RadioReceivingPriority> radioReceivingPriorities)
    {
        if (udpVoicePacket.Guid == _guid) //|| udpVoicePacket.OriginalClientGuid == _guid
            return;
        //my own transmission - throw away - stops test frequencies

        //Check if Global
        var globalFrequencies = _serverSettings.GlobalFrequencies;

        // filter radios by ability to hear it AND decryption works
        var retransmitOn = new List<RadioReceivingPriority>();
        //artificially limit some retransmissions - if encryption fails dont retransmit

        //from the subset of receiving radios - find any other radios that have retransmit - and dont retransmit on any with the same frequency
        //to stop loops
        //and ignore global frequencies 
        //and only if we can decrypt it (or no encryption)
        //and not received on Guard
        var receivingWithRetransmit = radioReceivingPriorities.Where(receivingRadio =>
            receivingRadio.ReceivingRadio.Retransmit
            //check global
            && !globalFrequencies.Any(freq =>
                RadioBase.FreqCloseEnough(receivingRadio.ReceivingRadio.Freq, freq))
            && !receivingRadio.ReceivingState.IsSecondary).ToList();

        //didnt receive on any radios that we could decrypt
        //stop
        if (receivingWithRetransmit.Count == 0) return;

        //radios able to retransmit
        var radiosWithRetransmit = _clientStateSingleton.PlayerUnitState.Radios.Where(radio => radio.Retransmit);

        //Check we're not retransmitting through a radio we just received on?
        foreach (var receivingRadio in receivingWithRetransmit)
            radiosWithRetransmit = radiosWithRetransmit.Where(radio =>
                !RadioBase.FreqCloseEnough(radio.Freq, receivingRadio.Frequency));

        var finalList = radiosWithRetransmit.ToList();

        if (finalList.Count == 0)
            //quit
            return;

        //From the remaining list - build up a new outgoing packet
        var frequencies = new double[finalList.Count];
        var encryptions = new byte[finalList.Count];
        var modulations = new byte[finalList.Count];

        for (var i = 0; i < finalList.Count; i++)
        {
            frequencies[i] = finalList[i].Freq;
            encryptions[i] = finalList[i].Encrypted ? finalList[i].EncKey : (byte)0;
            modulations[i] = (byte)finalList[i].Modulation;
        }

        //generate packet
        var relayedPacket = new UDPVoicePacket
        {
            AudioPart1Bytes = udpVoicePacket.AudioPart1Bytes,
            AudioPart1Length = udpVoicePacket.AudioPart1Length,
            Frequencies = frequencies,
            UnitId = _clientStateSingleton.PlayerUnitState.UnitId,
            Encryptions = encryptions,
            Modulations = modulations,
            PacketNumber = udpVoicePacket.PacketNumber,
            OriginalClientGuidBytes = udpVoicePacket.OriginalClientGuidBytes,
            RetransmissionCount = (byte)(udpVoicePacket.RetransmissionCount + 1u)
        };


        try
        {
            _udpClient.Send(relayedPacket);
        }
        catch (Exception)
        {
        }
    }

    private List<int> CurrentlyBlockedRadios()
    {
        var transmitting = new List<int>();
        if (!_serverSettings.GetSettingAsBool(ServerSettingsKeys.IRL_RADIO_TX)) return transmitting;

        if (!_ptt) return transmitting;

        //Currently transmitting - PTT must be true - figure out if we can hear on those radios

        var currentRadio =
            _clientStateSingleton.PlayerUnitState.Radios[_clientStateSingleton.PlayerUnitState.SelectedRadio];

        if (currentRadio.Modulation == Modulation.FM
            || currentRadio.Modulation == Modulation.AM
            || currentRadio.Modulation == Modulation.MIDS
            || currentRadio.Modulation == Modulation.HAVEQUICK)
            //only AM and FM block - SATCOM etc dont

            transmitting.Add(_clientStateSingleton.PlayerUnitState.SelectedRadio);


        if (_clientStateSingleton.PlayerUnitState.simultaneousTransmission)
            // Skip intercom
            for (var i = 1; i < 11; i++)
            {
                var radio = _clientStateSingleton.PlayerUnitState.Radios[i];
                if ((radio.Modulation == Modulation.FM || radio.Modulation == Modulation.AM) &&
                    radio.SimultaneousTransmission &&
                    i != _clientStateSingleton.PlayerUnitState.SelectedRadio)
                    transmitting.Add(i);
            }

        return transmitting;
    }

    private int SortRadioReceivingPriorities(RadioReceivingPriority x, RadioReceivingPriority y)
    {
        var xScore = 0;
        var yScore = 0;

        if (x.ReceivingRadio == null || x.ReceivingState == null) return 1;

        if ((y.ReceivingRadio == null) | (y.ReceivingState == null)) return -1;


        if (_clientStateSingleton.PlayerUnitState.SelectedRadio == x.ReceivingState.ReceivedOn) xScore += 8;

        if (_clientStateSingleton.PlayerUnitState.SelectedRadio == y.ReceivingState.ReceivedOn) yScore += 8;

        if (x.ReceivingRadio.Volume > 0) xScore += 4;

        if (y.ReceivingRadio.Volume > 0) yScore += 4;

        return yScore - xScore;
    }

    //TODO add support for HotMic and Intercom PTT
    //See UDPVoiceHandler in SRS
    private List<Radio> PTTPressed(out int sendingOn, bool voice)
    {
        sendingOn = -1;
        var transmittingRadios = new List<Radio>();
        var radioInfo = _clientStateSingleton.PlayerUnitState;
        //If its a hot intercom and thats not the currently selected radio
        //this is special logic currently for the gazelle as it has a hot mic, but no way of knowing if you're transmitting from the module itself
        //so we have to figure out what you're transmitting on in SRS
        if (radioInfo.IntercomHotMic
            && radioInfo.SelectedRadio != 0)
            if (radioInfo.Radios[0].Modulation == Modulation.INTERCOM)
            {
                //TODO check this
                // var intercom = new List<Radio>();
                transmittingRadios.Add(radioInfo.Radios[0]);
                sendingOn = 0;
                // return intercom;
            }


        if (_ptt || _udpCommandListener?.PTT == true)
        {
            // Always add currently selected radio (if valid)
            var currentSelected = _clientStateSingleton.PlayerUnitState.SelectedRadio;
            Radio currentlySelectedRadio = null;
            if (currentSelected >= 0
                && currentSelected < _clientStateSingleton.PlayerUnitState.Radios.Count)
            {
                currentlySelectedRadio = _clientStateSingleton.PlayerUnitState.Radios[currentSelected];

                if (currentlySelectedRadio != null && currentlySelectedRadio.Modulation !=
                                                   Modulation.DISABLED
                                                   && (currentlySelectedRadio.Freq > 100 ||
                                                       currentlySelectedRadio.Modulation ==
                                                       Modulation.INTERCOM))
                {
                    sendingOn = currentSelected;
                    transmittingRadios.Add(currentlySelectedRadio);
                }
            }

            // Add all radios toggled for simultaneous transmission if the global flag has been set
            if (_clientStateSingleton.PlayerUnitState.simultaneousTransmission)
            {
                //dont transmit on all if the INTERCOM is selected & AWACS
                if (currentSelected == 0 && currentlySelectedRadio.Modulation == Modulation.INTERCOM &&
                    _clientStateSingleton.PlayerUnitState.InAircraft == false)
                {
                    //even if simul transmission is enabled - if we're an AWACS we probably dont want this
                    var intercom = new List<Radio>();
                    intercom.Add(radioInfo.Radios[0]);
                    sendingOn = 0;
                    return intercom;
                }

                var i = 0;
                foreach (var radio in _clientStateSingleton.PlayerUnitState.Radios)
                {
                    if (radio != null && radio.SimultaneousTransmission && radio.Modulation != Modulation.DISABLED
                        && (radio.Freq > 100 || radio.Modulation == Modulation.INTERCOM)
                        && !transmittingRadios.Contains(radio)
                       ) // Make sure we don't add the selected radio twice
                    {
                        if (sendingOn == -1) sendingOn = i;

                        transmittingRadios.Add(radio);
                    }

                    i++;
                }
            }
        }

        return transmittingRadios;
    }

    public ClientAudio Send(byte[] bytes, int len, bool voice)
    {
        // List of radios the transmission is sent to (can me multiple if simultaneous transmission is enabled)
        List<Radio> transmittingRadios;
        //if either PTT is true, a microphone is available && socket connected etc
        var sendingOn = -1;
        if (_udpClient.Ready
            && bytes != null
            && (transmittingRadios = PTTPressed(out sendingOn, voice)).Count > 0)
            //can only send if DCS is connected
        {
            try
            {
                if (transmittingRadios.Count > 0)
                {
                    var frequencies = new List<double>(transmittingRadios.Count);
                    var encryptions = new List<byte>(transmittingRadios.Count);
                    var modulations = new List<byte>(transmittingRadios.Count);

                    for (var i = 0; i < transmittingRadios.Count; i++)
                    {
                        var radio = transmittingRadios[i];

                        // Further deduplicate transmitted frequencies if they have the same freq./modulation/encryption (caused by differently named radios)
                        var alreadyIncluded = false;
                        for (var j = 0; j < frequencies.Count; j++)
                            if (frequencies[j] == radio.Freq
                                && modulations[j] == (byte)radio.Modulation
                                && encryptions[j] == (radio.Encrypted ? radio.EncKey : 0))
                            {
                                alreadyIncluded = true;
                                break;
                            }

                        if (alreadyIncluded) continue;

                        frequencies.Add(radio.Freq);
                        encryptions.Add(radio.Encrypted ? radio.EncKey : (byte)0);
                        modulations.Add((byte)radio.Modulation);
                    }

                    //generate packet
                    var udpVoicePacket = new UDPVoicePacket
                    {
                        AudioPart1Bytes = bytes,
                        AudioPart1Length = (ushort)bytes.Length,
                        Frequencies = frequencies.ToArray(),
                        UnitId = _clientStateSingleton.PlayerUnitState.UnitId,
                        Encryptions = encryptions.ToArray(),
                        Modulations = modulations.ToArray(),
                        PacketNumber = _packetNumber++
                    };

                    _udpClient.Send(udpVoicePacket);

                    var currentlySelectedRadio = _clientStateSingleton.PlayerUnitState.Radios[sendingOn];

                    //not sending or really quickly switched sending
                    if (currentlySelectedRadio != null &&
                        (!_clientStateSingleton.RadioSendingState.IsSending ||
                         _clientStateSingleton.RadioSendingState.SendingOn != sendingOn))
                        _audioManager.PlaySoundEffectStartTransmit(sendingOn,
                            currentlySelectedRadio.Encrypted && currentlySelectedRadio.EncKey > 0,
                            currentlySelectedRadio.Volume, currentlySelectedRadio.Modulation);

                    //set radio overlay state
                    _clientStateSingleton.RadioSendingState = new RadioSendingState
                    {
                        IsSending = true,
                        LastSentAt = DateTime.Now.Ticks,
                        SendingOn = sendingOn
                    };
                    var send = new ClientAudio
                    {
                        Frequency = frequencies[0],
                        Modulation = modulations[0],
                        EncodedAudio = bytes,
                        Volume = 1,
                        ReceivedRadio = 1,
                        ReceiveTime = DateTime.Now.Ticks,
                        IsSecondary = false,
                        UnitType = _clientStateSingleton.PlayerUnitState.UnitType,
                        OriginalClientGuid = _guid,
                        ClientGuid = _guid,
                        UnitId = _clientStateSingleton.PlayerUnitState.UnitId
                    };
                    return send;
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Exception Sending Audio Message " + e.Message);
            }
        }
        else
        {
            if (_clientStateSingleton.RadioSendingState.IsSending)
            {
                _clientStateSingleton.RadioSendingState.IsSending = false;

                if (_clientStateSingleton.RadioSendingState.SendingOn >= 0)
                {
                    var radio = _clientStateSingleton.PlayerUnitState.Radios[
                        _clientStateSingleton.RadioSendingState.SendingOn];

                    _audioManager.PlaySoundEffectEndTransmit(_clientStateSingleton.RadioSendingState.SendingOn,
                        radio.Volume, radio.Modulation);
                }
            }
        }

        return null;
    }


    private void UdpAudioDecode()
    {
        try
        {
            while (!_stop)
                try
                {
                    var encodedOpusAudio = new byte[0];
                    _udpClient.EncodedAudio.TryTake(out encodedOpusAudio, 100000, _stopFlag.Token);

                    var time = DateTime.Now.Ticks; //should add at the receive instead?

                    if (encodedOpusAudio != null
                        && encodedOpusAudio.Length >=
                        UDPVoicePacket.PacketHeaderLength + UDPVoicePacket.FixedPacketLength +
                        UDPVoicePacket.FrequencySegmentLength)
                    {
                        //  process
                        // check if we should play audio

                        var myClient = IsClientMetaDataValid(_guid);

                        if (myClient != null && _clientStateSingleton.PlayerUnitState != null)
                        {
                            //Decode bytes
                            var udpVoicePacket = UDPVoicePacket.DecodeVoicePacket(encodedOpusAudio);

                            if (udpVoicePacket != null)
                            {
                                var globalFrequencies = _serverSettings.GlobalFrequencies;

                                var frequencyCount = udpVoicePacket.Frequencies.Length;

                                var radioReceivingPriorities =
                                    new List<RadioReceivingPriority>(frequencyCount);
                                var blockedRadios = CurrentlyBlockedRadios();

                                // Parse frequencies into receiving radio priority for selection below
                                for (var i = 0; i < frequencyCount; i++)
                                {
                                    RadioReceivingState state = null;

                                    bool decryptable;
                                    var radio = RadioBase.CanHearTransmission(
                                        udpVoicePacket.Frequencies[i],
                                        (Modulation)udpVoicePacket.Modulations[i],
                                        udpVoicePacket.Encryptions[i],
                                        udpVoicePacket.UnitId,
                                        blockedRadios,
                                        _clientStateSingleton.PlayerUnitState.BaseRadios,
                                        _clientStateSingleton.PlayerUnitState.UnitId,
                                        out state,
                                        out decryptable);


                                    if (radio != null && state != null
                                                      && (radio.Modulation == Modulation.INTERCOM
                                                          || !blockedRadios.Contains(state.ReceivedOn)))
                                    {
                                        //get the radio
                                        var receivedRadio =
                                            _clientStateSingleton.PlayerUnitState.Radios[state.ReceivedOn];

                                        radioReceivingPriorities.Add(new RadioReceivingPriority
                                        {
                                            Frequency = udpVoicePacket.Frequencies[i],
                                            LineOfSightLoss = 0,
                                            Modulation = udpVoicePacket.Modulations[i],
                                            ReceivingPowerLossPercent = 0,
                                            ReceivingRadio = receivedRadio,
                                            ReceivingState = state
                                        });
                                    }
                                }

                                // Sort receiving radios to play audio on correct one
                                radioReceivingPriorities.Sort(SortRadioReceivingPriorities);

                                if (radioReceivingPriorities.Count > 0)
                                {
                                    //ALL GOOD!
                                    //create marker for bytes
                                    for (var i = 0; i < radioReceivingPriorities.Count; i++)
                                    {
                                        var destinationRadio = radioReceivingPriorities[i];
                                        var isSimultaneousTransmission =
                                            radioReceivingPriorities.Count > 1 && i > 0;

                                        SRClientBase transmittingClient;
                                        _clients.TryGetValue(udpVoicePacket.Guid,
                                            out transmittingClient);

                                        var audio = new ClientAudio
                                        {
                                            ClientGuid = udpVoicePacket.Guid,
                                            EncodedAudio = udpVoicePacket.AudioPart1Bytes,
                                            //Convert to Shorts!
                                            ReceiveTime = DateTime.Now.Ticks,
                                            Frequency = destinationRadio.Frequency,
                                            Modulation = destinationRadio.Modulation,
                                            Volume = destinationRadio.ReceivingRadio.Volume,
                                            ReceivedRadio = destinationRadio.ReceivingState.ReceivedOn,
                                            UnitId = udpVoicePacket.UnitId,
                                            // mark if we can decrypt it
                                            RadioReceivingState = destinationRadio.ReceivingState,
                                            PacketNumber = udpVoicePacket.PacketNumber,
                                            OriginalClientGuid = udpVoicePacket.OriginalClientGuid,
                                            UnitType = transmittingClient?.UnitState?.UnitType
                                        };

                                        if (string.IsNullOrEmpty(audio.UnitType))
                                            audio.UnitType = PlayerUnitStateBase.TYPE_GROUND;

                                        var transmitterName = "";
                                        if (_serverSettings.GetSettingAsBool(ServerSettingsKeys
                                                .SHOW_TRANSMITTER_NAME)
                                            && _globalSettings.GetClientSettingBool(GlobalSettingsKeys
                                                .ShowTransmitterName)
                                           )
                                            //got it
                                            transmitterName = transmittingClient?.UnitState?.Name;


                                        var newRadioReceivingState = new RadioReceivingState
                                        {
                                            IsSecondary = destinationRadio.ReceivingState.IsSecondary,
                                            IsSimultaneous = isSimultaneousTransmission,
                                            LastRecievedAt = DateTime.Now.Ticks,
                                            PlayedEndOfTransmission = false,
                                            ReceivedOn = destinationRadio.ReceivingState.ReceivedOn,
                                            SentBy = transmitterName
                                        };

                                        _radioReceivingState[audio.ReceivedRadio] = newRadioReceivingState;

                                        //we now WANT to duplicate through multiple pipelines ONLY if AM blocking is on
                                        //this is a nice optimisation to save duplicated audio on servers without that setting 
                                        if (i == 0) _audioManager.AddClientAudio(audio);
                                    }

                                    //handle retransmission
                                    RetransmitAudio(udpVoicePacket, radioReceivingPriorities);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!_stop) Logger.Info(ex, "Failed to decode audio from Packet");
                }
        }
        catch (OperationCanceledException)
        {
            Logger.Info("Stopped DeJitter Buffer");
        }
    }

    private void PTTHandler(List<InputBindState> pressed)
    {
        var radios = _clientStateSingleton.PlayerUnitState;

        var radioSwitchPtt =
            _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioSwitchIsPTT);
        var radioSwitchPttWhenValid =
            _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys
                .RadioSwitchIsPTTOnlyWhenValid);

        //store the current PTT state and radios
        var currentRadioId = radios.SelectedRadio;
        var currentPtt = _ptt;

        var ptt = false;
        foreach (var inputBindState in pressed)
            if (inputBindState.IsActive)
            {
                //radio switch?
                if ((int)inputBindState.MainDevice.InputBind >= (int)InputBinding.Intercom &&
                    (int)inputBindState.MainDevice.InputBind <= (int)InputBinding.Switch10)
                {
                    //gives you radio id if you minus 100
                    var radioId = (int)inputBindState.MainDevice.InputBind - 100;

                    if (radioId < _clientStateSingleton.PlayerUnitState.Radios.Count)
                    {
                        var clientRadio = _clientStateSingleton.PlayerUnitState.Radios[radioId];

                        if (RadioHelper.SelectRadio(radioId))
                        {
                            //turn on PTT
                            if (radioSwitchPttWhenValid || radioSwitchPtt)
                            {
                                _lastPTTPress = DateTime.Now.Ticks;
                                ptt = true;
                                //Store last release time
                            }
                        }
                        else
                        {
                            //turn on PTT even if not valid radio switch
                            if (radioSwitchPtt)
                            {
                                _lastPTTPress = DateTime.Now.Ticks;
                                ptt = true;
                            }
                        }
                    }
                }
                else if (inputBindState.MainDevice.InputBind == InputBinding.Ptt)
                {
                    _lastPTTPress = DateTime.Now.Ticks;
                    ptt = true;
                }
            }

        /**
     * Handle DELAYING PTT START
     */

        if (!ptt)
            //reset
            _firstPTTPress = -1;

        if (_firstPTTPress == -1 && ptt) _firstPTTPress = DateTime.Now.Ticks;

        if (ptt)
        {
            //should inhibit for a bit
            var startDiff = new TimeSpan(DateTime.Now.Ticks - _firstPTTPress);

            var startInhibit = _globalSettings.ProfileSettingsStore
                .GetClientSettingFloat(ProfileSettingsKeys.PTTStartDelay);

            if (startDiff.TotalMilliseconds < startInhibit)
            {
                _ptt = false;
                _lastPTTPress = -1;
                return;
            }
        }

        /**
         * End Handle DELAYING PTT START
         */


        /**
         * Start Handle PTT HOLD after release
         */

        //if length is zero - no keybinds or no PTT pressed set to false
        var diff = new TimeSpan(DateTime.Now.Ticks - _lastPTTPress);

        //Release the PTT ONLY if X ms have passed and we didnt switch radios to handle
        //shitty buttons
        var releaseTime = _globalSettings.ProfileSettingsStore
            .GetClientSettingFloat(ProfileSettingsKeys.PTTReleaseDelay);

        if (!ptt
            && releaseTime > 0
            && diff.TotalMilliseconds <= releaseTime
            && currentRadioId == radios.SelectedRadio)
            ptt = true;

        /**
         * End Handle PTT HOLD after release
         */

        _ptt = ptt;

        //TEMP TODO
        //_ptt = true;
    }

    public void Stop()
    {
        lock (lockObj)
        {
            _stop = true;
            _stopFlag.Cancel();
            _clientStateSingleton.RadioSendingState.IsSending = false;
            _udpCommandListener?.Stop();
            _udpCommandListener = null;
            _udpStateSender?.Stop();
            _udpStateSender = null;
            InputDeviceManager.Instance.StopListening();
        }
    }
}