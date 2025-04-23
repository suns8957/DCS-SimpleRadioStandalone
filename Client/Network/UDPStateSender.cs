using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Ciribob.FS3D.SimpleRadio.Standalone.Client.Settings;
using Ciribob.FS3D.SimpleRadio.Standalone.Client.Singletons;
using Newtonsoft.Json;
using NLog;

namespace Ciribob.FS3D.SimpleRadio.Standalone.Client.Network;

public class UDPStateSender
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;
    private volatile bool _stop;
    private UdpClient _udpStateSenderSocket;

    public void Start()
    {
        var thread = new Thread(StartStateSender);
        thread.Start();
    }

    private void StartStateSender()
    {
        var endPoint = new IPEndPoint(IPAddress.Loopback,
            _globalSettings.GetClientSetting(GlobalSettingsKeys.UDPExternalSenderPort).IntValue);

        _udpStateSenderSocket = new UdpClient();

        while (!_stop)
            try
            {
                //Generate Current State
                var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(GenerateExternalStateMessage()) + "\n");
                _udpStateSenderSocket.Send(bytes, bytes.Length, endPoint);

                Thread.Sleep(100);
            }
            catch (SocketException e)
            {
                // SocketException is raised when closing app/disconnecting, ignore so we don't log "irrelevant" exceptions
                if (!_stop) Logger.Error(e, "SocketException Sending UDP State Message");
            }
            catch (Exception e)
            {
                Logger.Error(e, "Exception Handling UDP State Message");
            }

        try
        {
            _udpStateSenderSocket.Close();
        }
        catch (Exception e)
        {
            Logger.Error(e, "Exception stoping UDP State Sender");
        }
    }

    private UDPExternalStateMessage GenerateExternalStateMessage()
    {
        var radios = new List<UDPExternalRadioState>();

        var index = 0;
        foreach (var radio in ClientStateSingleton.Instance.PlayerUnitState.Radios)
        {
            var receivingState = ClientStateSingleton.Instance.RadioReceivingState[index];

            var externalRadio = new UDPExternalRadioState
            {
                Id = index,
                ChnName = radio?.CurrentChannel?.Text,
                Freq = radio.Freq,
                SecFreq = radio.SecFreq,
                Vol = radio.Volume,
                Rx = receivingState.IsReceiving,
                Mode = radio.Modulation
            };

            if (ClientStateSingleton.Instance.RadioSendingState.IsSending &&
                ClientStateSingleton.Instance.RadioSendingState.SendingOn == index)
                externalRadio.Tx = true;
            else
                externalRadio.Tx = false;

            if (radio.CurrentChannel != null) externalRadio.Chn = radio.CurrentChannel.Channel;

            radios.Add(externalRadio);

            index++;
        }


        return new UDPExternalStateMessage
        {
            Radios = radios,
            Name = ClientStateSingleton.Instance.PlayerUnitState.Name,
            UnitId = ClientStateSingleton.Instance.PlayerUnitState.UnitId,
            SelectedRadio = ClientStateSingleton.Instance.PlayerUnitState.SelectedRadio
        };
    }

    public void Stop()
    {
        _stop = true;
    }
}