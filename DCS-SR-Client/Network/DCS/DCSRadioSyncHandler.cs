using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS.Models.DCSState;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Utils;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using Newtonsoft.Json;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS;

public class DCSRadioSyncHandler
{
    public delegate void NewAircraft(string name, int seat);

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static readonly int RADIO_UPDATE_PING_INTERVAL = 60; //send update regardless of change every X seconds
    private readonly ConnectedClientsSingleton _clients = ConnectedClientsSingleton.Instance;

    private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;

    private readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;

    private readonly SyncedServerSettings _serverSettings = SyncedServerSettings.Instance;
    private UdpClient _dcsRadioUpdateSender;


    private UdpClient _dcsUdpListener;

    private long _identStart;

    private volatile bool _stop;

    public DCSRadioSyncHandler()
    {
    }

    public void Start()
    {
        //reset last sent
        _clientStateSingleton.LastSent = 0;

        Task.Factory.StartNew(() =>
        {
            while (!_stop)
                try
                {
                    var localEp = new IPEndPoint(IPAddress.Any,
                        _globalSettings.GetNetworkSetting(GlobalSettingsKeys.DCSIncomingUDP));
                    _dcsUdpListener = new UdpClient(localEp);
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex,
                        $"Unable to bind to the DCS Export Listener Socket Port: {_globalSettings.GetNetworkSetting(GlobalSettingsKeys.DCSIncomingUDP)}");
                    Thread.Sleep(500);
                }

            while (!_stop)
                try
                {
                    var groupEp = new IPEndPoint(IPAddress.Any, 0);
                    var bytes = _dcsUdpListener.Receive(ref groupEp);

                    var str = Encoding.UTF8.GetString(
                        bytes, 0, bytes.Length).Trim();

                    var message =
                        JsonConvert.DeserializeObject<DCSPlayerRadioInfo>(str);

                    Logger.Debug($"Recevied Message from DCS {str}");

                    if (!string.IsNullOrWhiteSpace(message.name) && message.name != "Unknown" &&
                        message.name != _clientStateSingleton.LastSeenName)
                        _clientStateSingleton.LastSeenName = message.name;

                    _clientStateSingleton.DcsExportLastReceived = DateTime.Now.Ticks;

                    //sync with others
                    //Radio info is marked as Stale for FC3 aircraft after every frequency change
                    ProcessRadioInfo(message);
                }
                catch (SocketException e)
                {
                    // SocketException is raised when closing app/disconnecting, ignore so we don't log "irrelevant" exceptions
                    if (!_stop) Logger.Error(e, "SocketException Handling DCS Message");
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Exception Handling DCS Message");
                }

            try
            {
                _dcsUdpListener.Close();
            }
            catch (Exception e)
            {
                Logger.Error(e, "Exception stopping DCS listener ");
            }
        });
    }


    public void ProcessRadioInfo(DCSPlayerRadioInfo message)
    {
        // determine if its changed by comparing old to new
        var update = UpdateRadio(message);

        //send to DCS UI
        SendRadioUpdateToDCS();

        Logger.Debug("Update sent to DCS");

        var diff = new TimeSpan(DateTime.Now.Ticks - _clientStateSingleton.LastSent);
        
        if (update
            || _clientStateSingleton.LastSent < 1
            || diff.TotalSeconds > 60)
        {
            Logger.Debug("Sending Radio Info To Server - Update");
            _clientStateSingleton.LastSent = DateTime.Now.Ticks;

   
            //TODO do this through the singleton so its not a mess
            //Full Update send over TCP
            EventBus.Instance.PublishOnCurrentThreadAsync(new UnitUpdateMessage()
            {
                FullUpdate = true,
                UnitUpdate = new SRClientBase()
                {
                    RadioInfo = _clientStateSingleton.DcsPlayerRadioInfo.ConvertToRadioBase(),
                    ClientGuid = _clientStateSingleton.ShortGUID,
                    Coalition = _clientStateSingleton.PlayerCoaltionLocationMetadata.side,
                    LatLngPosition = _clientStateSingleton.PlayerCoaltionLocationMetadata.LngLngPosition,
                    Seat = _clientStateSingleton.PlayerCoaltionLocationMetadata.seat,
                    Name = _clientStateSingleton.LastSeenName,
                    AllowRecord = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.AllowRecording)
                }
            });
        }
    }

    //send updated radio info back to DCS for ingame GUI
    private void SendRadioUpdateToDCS()
    {
        if (_dcsRadioUpdateSender == null) _dcsRadioUpdateSender = new UdpClient();

        try
        {
            var tunedClients = new int[11];

            if (_clientStateSingleton.IsConnected
                && _clientStateSingleton.DcsPlayerRadioInfo != null
                && _clientStateSingleton.DcsPlayerRadioInfo.IsCurrent())
                for (var i = 0; i < tunedClients.Length; i++)
                {
                    var clientRadio = _clientStateSingleton.DcsPlayerRadioInfo.radios[i];

                    if (clientRadio.modulation != Modulation.DISABLED)
                        tunedClients[i] =
                            _clientStateSingleton.ClientsOnFreq(clientRadio.freq, clientRadio.modulation);
                }

            //get currently transmitting or receiving
            var combinedState = new CombinedRadioState
            {
                RadioInfo = _clientStateSingleton.DcsPlayerRadioInfo,
                RadioSendingState = _clientStateSingleton.RadioSendingState,
                RadioReceivingState = _clientStateSingleton.RadioReceivingState,
                ClientCountConnected = _clients.Total,
                TunedClients = tunedClients
            };

            var message = JsonConvert.SerializeObject(combinedState, new JsonSerializerSettings
            {
                //   NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new JsonDCSPropertiesResolver()
            }) + "\n";

            var byteData =
                Encoding.UTF8.GetBytes(message);

            //Logger.Info("Sending Update over UDP 7080 DCS - 7082 Flight Panels: \n"+message);

            _dcsRadioUpdateSender.Send(byteData, byteData.Length,
                new IPEndPoint(IPAddress.Parse("127.0.0.1"),
                    _globalSettings.GetNetworkSetting(GlobalSettingsKeys.OutgoingDCSUDPInfo))); //send to DCS
            _dcsRadioUpdateSender.Send(byteData, byteData.Length,
                new IPEndPoint(IPAddress.Parse("127.0.0.1"),
                    _globalSettings.GetNetworkSetting(GlobalSettingsKeys
                        .OutgoingDCSUDPOther))); // send to Flight Control Panels
        }
        catch (Exception e)
        {
            Logger.Error(e, "Exception Sending DCS Radio Update Message");
        }
    }

    private bool UpdateRadio(DCSPlayerRadioInfo message)
    {
        var expansion = _serverSettings.GetSettingAsBool(ServerSettingsKeys.RADIO_EXPANSION);

        if (expansion)
            //override the server side setting
            expansion = !_globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys
                .DisableExpansionRadios);

        var playerRadioInfo = _clientStateSingleton.DcsPlayerRadioInfo;

        //copy and compare to look for changes
        var beforeUpdate = playerRadioInfo.DeepClone();

        //update common parts
        playerRadioInfo.name = message.name;
        playerRadioInfo.inAircraft = message.inAircraft;
        playerRadioInfo.intercomHotMic = message.intercomHotMic;
        playerRadioInfo.capabilities = message.capabilities;
        playerRadioInfo.ambient = message.ambient;

        //round volume to nearest 10% to reduce messages?
        //TODO move this out to be like position so its a special small update?
        //TODO check this
        playerRadioInfo.ambient.vol =
            message.ambient
                .vol; // (float)(Math.Round(playerRadioInfo.ambient.vol / 10f, MidpointRounding.AwayFromZero) * 10.0f);

        if (_globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.AlwaysAllowHotasControls))
        {
            message.control = DCSPlayerRadioInfo.RadioSwitchControls.HOTAS;
            playerRadioInfo.control = DCSPlayerRadioInfo.RadioSwitchControls.HOTAS;
        }
        else
        {
            playerRadioInfo.control = message.control;
        }

        playerRadioInfo.simultaneousTransmissionControl = message.simultaneousTransmissionControl;

        playerRadioInfo.unit = message.unit;

        if (!_clientStateSingleton.ShouldUseLotATCPosition())
            _clientStateSingleton.UpdatePlayerPosition(message.latLng);

        var overrideFreqAndVol = false;

        var newAircraft = playerRadioInfo.unitId != message.unitId || playerRadioInfo.seat != message.seat ||
                          !playerRadioInfo.IsCurrent();

        overrideFreqAndVol = playerRadioInfo.unitId != message.unitId;

        //save unit id
        playerRadioInfo.unitId = message.unitId;
        playerRadioInfo.seat = message.seat;


        if (newAircraft)
        {
            if (_globalSettings.GetClientSettingBool(GlobalSettingsKeys.AutoSelectSettingsProfile))
            {
                //TODO handle profile selection when switching aircraft
                //  _newAircraftCallback(message.unit, message.seat);
                //send message to UI thread on event bus and switch the profile
                EventBus.Instance.PublishOnUIThreadAsync(new NewUnitEnteredMessage()
                {
                    Unit = message.unit,
                    Seat = message.seat
                });
            }


            playerRadioInfo.iff = message.iff;
        }

        if (overrideFreqAndVol) playerRadioInfo.selected = message.selected;

        if (playerRadioInfo.control == DCSPlayerRadioInfo.RadioSwitchControls.IN_COCKPIT)
            playerRadioInfo.selected = message.selected;

        var simul = false;


        //copy over radio names, min + max
        for (var i = 0; i < playerRadioInfo.radios.Length; i++)
        {
            var clientRadio = playerRadioInfo.radios[i];

            //if we have more radios than the message has
            if (i >= message.radios.Length)
            {
                clientRadio.freq = 1;
                clientRadio.freqMin = 1;
                clientRadio.freqMax = 1;
                clientRadio.secFreq = 0;
                clientRadio.retransmit = false;
                clientRadio.modulation = Modulation.DISABLED;
                clientRadio.name = "No Radio";
                clientRadio.rtMode = DCSRadio.RetransmitMode.DISABLED;
                clientRadio.retransmit = false;

                clientRadio.freqMode = DCSRadio.FreqMode.COCKPIT;
                clientRadio.guardFreqMode = DCSRadio.FreqMode.COCKPIT;
                clientRadio.encMode = DCSRadio.EncryptionMode.NO_ENCRYPTION;
                clientRadio.volMode = DCSRadio.VolumeMode.COCKPIT;
                clientRadio.rxOnly = false;

                continue;
            }

            var updateRadio = message.radios[i];


            if ((updateRadio.expansion && !expansion) ||
                updateRadio.modulation == Modulation.DISABLED)
            {
                //expansion radio, not allowed
                clientRadio.freq = 1;
                clientRadio.freqMin = 1;
                clientRadio.freqMax = 1;
                clientRadio.secFreq = 0;
                clientRadio.retransmit = false;
                clientRadio.modulation = Modulation.DISABLED;
                clientRadio.name = "No Radio";
                clientRadio.rtMode = DCSRadio.RetransmitMode.DISABLED;
                clientRadio.retransmit = false;

                clientRadio.freqMode = DCSRadio.FreqMode.COCKPIT;
                clientRadio.guardFreqMode = DCSRadio.FreqMode.COCKPIT;
                clientRadio.encMode = DCSRadio.EncryptionMode.NO_ENCRYPTION;
                clientRadio.volMode = DCSRadio.VolumeMode.COCKPIT;
                clientRadio.rxOnly = false;
            }
            else
            {
                //update common parts
                clientRadio.freqMin = updateRadio.freqMin;
                clientRadio.freqMax = updateRadio.freqMax;

                if (playerRadioInfo.simultaneousTransmissionControl ==
                    DCSPlayerRadioInfo.SimultaneousTransmissionControl.EXTERNAL_DCS_CONTROL)
                    clientRadio.simul = updateRadio.simul;

                if (updateRadio.simul) simul = true;

                clientRadio.name = updateRadio.name;

                clientRadio.modulation = updateRadio.modulation;

                //update modes
                clientRadio.freqMode = updateRadio.freqMode;
                clientRadio.guardFreqMode = updateRadio.guardFreqMode;
                clientRadio.rtMode = updateRadio.rtMode;
                clientRadio.rxOnly = updateRadio.rxOnly;

                if (_serverSettings.GetSettingAsBool(ServerSettingsKeys.ALLOW_RADIO_ENCRYPTION))
                    clientRadio.encMode = updateRadio.encMode;
                else
                    clientRadio.encMode = DCSRadio.EncryptionMode.NO_ENCRYPTION;

                clientRadio.volMode = updateRadio.volMode;

                if (updateRadio.freqMode == DCSRadio.FreqMode.COCKPIT || overrideFreqAndVol)
                {
                    clientRadio.freq = updateRadio.freq;

                    if (newAircraft && updateRadio.guardFreqMode == DCSRadio.FreqMode.OVERLAY)
                    {
                        //default guard to off
                        clientRadio.secFreq = 0;
                    }
                    else
                    {
                        if (clientRadio.secFreq != 0 && updateRadio.guardFreqMode == DCSRadio.FreqMode.OVERLAY)
                            //put back
                            clientRadio.secFreq = updateRadio.secFreq;
                        else if (clientRadio.secFreq == 0 &&
                                 updateRadio.guardFreqMode == DCSRadio.FreqMode.OVERLAY)
                            clientRadio.secFreq = 0;
                        else
                            clientRadio.secFreq = updateRadio.secFreq;
                    }

                    clientRadio.channel = updateRadio.channel;
                }
                else
                {
                    if (clientRadio.secFreq != 0)
                        //put back
                        clientRadio.secFreq = updateRadio.secFreq;

                    //check we're not over a limit
                    if (clientRadio.freq > clientRadio.freqMax)
                        clientRadio.freq = clientRadio.freqMax;
                    else if (clientRadio.freq < clientRadio.freqMin) clientRadio.freq = clientRadio.freqMin;
                }

                //reset encryption
                if (overrideFreqAndVol)
                {
                    clientRadio.enc = false;
                    clientRadio.encKey = 0;
                }

                //Handle Encryption
                if (updateRadio.encMode == DCSRadio.EncryptionMode.ENCRYPTION_JUST_OVERLAY)
                {
                    if (clientRadio.encKey == 0) clientRadio.encKey = 1;
                }
                else if (clientRadio.encMode ==
                         DCSRadio.EncryptionMode.ENCRYPTION_COCKPIT_TOGGLE_OVERLAY_CODE)
                {
                    clientRadio.enc = updateRadio.enc;

                    if (clientRadio.encKey == 0) clientRadio.encKey = 1;
                }
                else if (clientRadio.encMode == DCSRadio.EncryptionMode.ENCRYPTION_FULL)
                {
                    clientRadio.enc = updateRadio.enc;
                    clientRadio.encKey = updateRadio.encKey;
                }
                else
                {
                    clientRadio.enc = false;
                    clientRadio.encKey = 0;
                }

                //handle volume
                if (updateRadio.volMode == DCSRadio.VolumeMode.COCKPIT || overrideFreqAndVol)
                    clientRadio.volume = updateRadio.volume;

                //handle Retransmit mode
                if (updateRadio.rtMode == DCSRadio.RetransmitMode.COCKPIT)
                {
                    clientRadio.rtMode = updateRadio.rtMode;
                    clientRadio.retransmit = updateRadio.retransmit;
                }
                else if (updateRadio.rtMode == DCSRadio.RetransmitMode.DISABLED)
                {
                    clientRadio.rtMode = updateRadio.rtMode;
                    clientRadio.retransmit = false;
                }

                //handle Channels load for radios
                if (newAircraft && i > 0)
                {
                    if (clientRadio.freqMode == DCSRadio.FreqMode.OVERLAY)
                    {
                        var channelModel = _clientStateSingleton.FixedChannels[i - 1];
                        channelModel.Max = clientRadio.freqMax;
                        channelModel.Min = clientRadio.freqMin;
                        channelModel.Reload();
                        var preselectedChannel = clientRadio.channel;

                        clientRadio.channel = -1; //reset channel

                        if (_globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys
                                .AutoSelectPresetChannel))
                            RadioHelper.RadioChannelUp(i);
                        else if (_clientStateSingleton.ExternalAWACSModeConnected)
                            if (preselectedChannel != -1)
                            {
                                // Keep whatever channel they preset in the json when in EAM mode.
                                channelModel.SelectedPresetChannel =
                                    channelModel.PresetChannels[preselectedChannel - 1];
                                RadioHelper.SelectRadioChannel(channelModel.SelectedPresetChannel, i);
                            }
                    }
                    else
                    {
                        _clientStateSingleton.FixedChannels[i - 1].Clear();
                        //clear
                    }
                }
            }
        }


        if (playerRadioInfo.simultaneousTransmissionControl ==
            DCSPlayerRadioInfo.SimultaneousTransmissionControl.EXTERNAL_DCS_CONTROL)
            playerRadioInfo.simultaneousTransmission = simul;

        //change PTT last
        if (!_globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.AllowDCSPTT))
            playerRadioInfo.ptt = false;
        else
            playerRadioInfo.ptt = message.ptt;

        //HANDLE IFF/TRANSPONDER UPDATE
        //TODO tidy up the IFF/Transponder handling and this giant function in general as its silly big :(


        if (_globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys
                .AlwaysAllowTransponderOverlay))
            if (message.iff.control != Transponder.IFFControlMode.DISABLED)
            {
                playerRadioInfo.iff.control = Transponder.IFFControlMode.OVERLAY;
                message.iff.control = Transponder.IFFControlMode.OVERLAY;
            }

        if (message.iff.control == Transponder.IFFControlMode.COCKPIT) playerRadioInfo.iff = message.iff;


        //HANDLE MIC IDENT
        if (!playerRadioInfo.ptt && playerRadioInfo.iff.mic > 0 && _clientStateSingleton.RadioSendingState.IsSending)
            if (_clientStateSingleton.RadioSendingState.SendingOn == playerRadioInfo.iff.mic)
                playerRadioInfo.iff.status = Transponder.IFFStatus.IDENT;

        //Handle IDENT only lasting for 40 seconds at most - need to toggle it
        if (playerRadioInfo.iff.status == Transponder.IFFStatus.IDENT)
        {
            if (_identStart == 0) _identStart = DateTime.Now.Ticks;

            if (TimeSpan.FromTicks(DateTime.Now.Ticks - _identStart).TotalSeconds > 40)
                playerRadioInfo.iff.status = Transponder.IFFStatus.NORMAL;
        }
        else
        {
            _identStart = 0;
        }

        //                }
        //            }

        //update
        playerRadioInfo.LastUpdate = DateTime.Now.Ticks;

        return !beforeUpdate.Equals(playerRadioInfo);
    }

    public void Stop()
    {
        _stop = true;
        try
        {
            _dcsUdpListener?.Close();
        }
        catch (Exception)
        {
        }

        try
        {
            _dcsRadioUpdateSender?.Close();
        }
        catch (Exception)
        {
            //IGNORE
        }

        _clientStateSingleton.DcsExportLastReceived = -1;
    }
}