using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Ciribob.FS3D.SimpleRadio.Standalone.Client.Settings;
using Ciribob.FS3D.SimpleRadio.Standalone.Client.Utils;
using Newtonsoft.Json;
using NLog;

namespace Ciribob.FS3D.SimpleRadio.Standalone.Client.Network;

public class UDPCommandListener
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;
    private volatile bool _stop;
    private UdpClient _udpCommandListener;

    public bool PTT { get; set; }

    public void Start()
    {
        var thread = new Thread(StartUDPCommandListener);
        thread.Start();
    }

    private void StartUDPCommandListener()
    {
        while (!_stop)
            try
            {
                var localEp = new IPEndPoint(IPAddress.Any,
                    _globalSettings.GetNetworkSetting(GlobalSettingsKeys.UDPCommandListenerPort));
                _udpCommandListener = new UdpClient(localEp);
                break;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex,
                    $"Unable to bind to the UDP Command Listener Socket Port: {_globalSettings.GetNetworkSetting(GlobalSettingsKeys.UDPCommandListenerPort)}");
                Thread.Sleep(500);
            }

        while (!_stop)
            try
            {
                var groupEp = new IPEndPoint(IPAddress.Any, 0);
                var bytes = _udpCommandListener.Receive(ref groupEp);

                Logger.Debug("Received Message from UDP COMMAND INTERFACE: " + Encoding.UTF8.GetString(
                    bytes, 0, bytes.Length));
                var message =
                    JsonConvert.DeserializeObject<UDPInterfaceCommand>(Encoding.UTF8.GetString(
                        bytes, 0, bytes.Length));

                if (message?.Command == UDPInterfaceCommand.TX_END)
                {
                    PTT = false;
                    //Send TX_END
                    RadioHelper.SelectRadio(message.RadioId);
                }
                else if (message?.Command == UDPInterfaceCommand.TX_START)
                {
                    //Send TX_START
                    RadioHelper.SelectRadio(message.RadioId);
                    PTT = true;
                }
                else if (message?.Command == UDPInterfaceCommand.VOLUME)
                {
                    RadioHelper.SelectRadio(message.RadioId);

                    if (message?.Parameter == 10)
                        RadioHelper.SetRadioVolume(1.0f, message.RadioId);
                    else if (message?.Parameter == 0) RadioHelper.SetRadioVolume(0, message.RadioId);
                }
                else
                {
                    Logger.Error("Unknown UDP Command!");
                }
            }
            catch (SocketException e)
            {
                // SocketException is raised when closing app/disconnecting, ignore so we don't log "irrelevant" exceptions
                if (!_stop) Logger.Error(e, "SocketException Handling UDP Message");
            }
            catch (Exception e)
            {
                Logger.Error(e, "Exception Handling UDP Message");
            }

        try
        {
            _udpCommandListener.Close();
        }
        catch (Exception e)
        {
            Logger.Error(e, "Exception stoping UDP Command listener ");
        }
    }

    public void Stop()
    {
        _stop = true;

        try
        {
            _udpCommandListener?.Close();
        }
        catch (Exception ex)
        {
        }
    }
}