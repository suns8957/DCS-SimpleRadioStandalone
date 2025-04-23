using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Models.EventMessages;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using Newtonsoft.Json;
using NLog;
using LogManager = NLog.LogManager;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Client;

public class TCPClientHandler : IHandle<DisconnectRequestMessage>, IHandle<UnitUpdateMessage>
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static readonly int MAX_DECODE_ERRORS = 5;
    private readonly ConnectedClientsSingleton _clients = ConnectedClientsSingleton.Instance;
    private readonly string _guid;

    private readonly SyncedServerSettings _serverSettings = SyncedServerSettings.Instance;

    //   private UDPVoiceHandler _udpVoiceHandler;

    private bool _connected = false;

    private long _lastSent = -1;
    private SRClientBase _playerUnitState;
    private IPEndPoint _serverEndpoint;

    private volatile bool _stop;
    private TcpClient _tcpClient;

    public TCPClientHandler(string guid, SRClientBase playerUnitState)
    {
        _clients.Clear();
        _guid = guid;
        _playerUnitState = playerUnitState;
    }

    public bool TCPConnected
    {
        get
        {
            if (_tcpClient != null) return _tcpClient.Connected;
            return false;
        }
    }

    public Task HandleAsync(DisconnectRequestMessage message, CancellationToken cancellationToken)
    {
        Disconnect();

        return Task.CompletedTask;
    }

    public Task HandleAsync(UnitUpdateMessage message, CancellationToken cancellationToken)
    {
        if (message.FullUpdate)
            ClientRadioUpdated(message.UnitUpdate);
        else
            ClientCoalitionUpdate(message.UnitUpdate);

        return Task.CompletedTask;
    }

    public void TryConnect(IPEndPoint endpoint)
    {
        _serverEndpoint = endpoint;

        Logger.Info($"TryConnect @ {_serverEndpoint}");
        var tcpThread = new Thread(Connect);
        tcpThread.Start();
    }

    private void Connect()
    {
        _lastSent = DateTime.Now.Ticks;

        var connectionError = false;

        using (_tcpClient = new TcpClient())
        {
            try
            {
                _tcpClient.SendTimeout = 90000;
                _tcpClient.NoDelay = true;

                // Wait for 10 seconds before aborting connection attempt - no SRS server running/port opened in that case
                _tcpClient.ConnectAsync(_serverEndpoint.Address, _serverEndpoint.Port)
                    .Wait(TimeSpan.FromSeconds(10));

                if (_tcpClient.Connected)
                {
                    _tcpClient.NoDelay = true;

                    _tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

                    ClientSyncLoop(_playerUnitState);
                }
                else
                {
                    Logger.Error($"Failed to connect to server @ {_serverEndpoint}");
                    // Signal disconnect including an error
                    connectionError = true;
                    EventBus.Instance.PublishOnUIThreadAsync(new TCPClientStatusMessage(false,
                        TCPClientStatusMessage.ErrorCode.TIMEOUT));
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Could not connect to server");
                connectionError = true;
                Disconnect();
            }
        }
    }

    private void ClientRadioUpdated(SRClientBase updatedUnitState)
    {
        Logger.Debug("Sending Full Update to Server");

        //Only send if there is an actual change
        if (!updatedUnitState.Equals(_playerUnitState))
        {
            _playerUnitState = updatedUnitState;
            var message = new NetworkMessage
            {
                Client = updatedUnitState,
                MsgType = NetworkMessage.MessageType.RADIO_UPDATE
            };

            SendToServer(message);
        }
    }

    private void ClientCoalitionUpdate(SRClientBase updatedMetadata)
    {
        //only send if there is an actual change to metadata
        if (!_playerUnitState.MetaDataEquals(updatedMetadata))
        {
            //TODO update here
            // _playerUnitState.UpdateMetadata(updatedMetadata);
            //dont send radios to cut down size
            // updatedMetadata.Radios = new List<RadioBase>();

            var message = new NetworkMessage
            {
                Client = updatedMetadata,
                MsgType = NetworkMessage.MessageType.UPDATE
            };

            //update state
            SendToServer(message);
        }
    }


    private void ClientSyncLoop(SRClientBase initialState)
    {
        EventBus.Instance.SubscribeOnBackgroundThread(this);
        //clear the clients list
        _clients.Clear();
        var decodeErrors = 0; //if the JSON is unreadable - new version likely

        using (var reader = new StreamReader(_tcpClient.GetStream(), Encoding.UTF8))
        {
            try
            {
                //TODO switch to proxy for everything
                //TODO remove _clientstate and just pass in the initial state
                //then use broadcasts / events for the rest

                //start the loop off by sending a SYNC Request
                SendToServer(new NetworkMessage
                {
                    Client = initialState,
                    MsgType = NetworkMessage.MessageType.SYNC
                });

                EventBus.Instance.PublishOnUIThreadAsync(new TCPClientStatusMessage(true, _serverEndpoint));

                string line;
                while ((line = reader.ReadLine()) != null)
                    try
                    {
                        var serverMessage = JsonConvert.DeserializeObject<NetworkMessage>(line);
                        decodeErrors = 0; //reset counter
                        if (serverMessage != null)
                            //Logger.Debug("Received "+serverMessage.MsgType);
                            switch (serverMessage.MsgType)
                            {
                                case NetworkMessage.MessageType.PING:
                                    // Do nothing for now
                                    break;
                                case NetworkMessage.MessageType.RADIO_UPDATE:
                                case NetworkMessage.MessageType.UPDATE:

                                    if (serverMessage.ServerSettings != null)
                                        _serverSettings.Decode(serverMessage.ServerSettings);

                                    SRClientBase srClient;
                                    if (_clients.TryGetValue(serverMessage.Client.ClientGuid, out srClient))
                                    {
                                        if (serverMessage.MsgType == NetworkMessage.MessageType.RADIO_UPDATE)
                                            HandleFullUpdate(serverMessage, srClient);
                                        else if (serverMessage.MsgType == NetworkMessage.MessageType.UPDATE)
                                            HandlePartialUpdate(serverMessage, srClient);
                                    }
                                    else
                                    {
                                        var connectedClient = serverMessage.Client;
                                        connectedClient.LastUpdate = DateTime.Now.Ticks;

                                        //init with LOS true so you can hear them incase of bad DCS install where
                                        //LOS isnt working
                                        connectedClient.LineOfSightLoss = 0.0f;
                                        //0.0 is NO LOSS therefore full Line of sight

                                        _clients[serverMessage.Client.ClientGuid] = connectedClient;

                                        srClient = connectedClient;

                                        // Logger.Debug("Received New Client: " + NetworkMessage.MessageType.UPDATE +
                                        //             " From: " +
                                        //             serverMessage.Client.Name + " Coalition: " +
                                        //             serverMessage.Client.Coalition);
                                    }

                                    //TODO handle EAM disconnect
                                    // if (_clientStateSingleton.ExternalAWACSModelSelected &&
                                    //     !_serverSettings.GetSettingAsBool(Common.Setting.ServerSettingsKeys.EXTERNAL_AWACS_MODE))
                                    // {
                                    //     DisconnectExternalAWACSMode();
                                    // }

                                    srClient.LastUpdate = DateTime.Now.Ticks;
                                    EventBus.Instance.PublishOnUIThreadAsync(new SRClientUpdateMessage(srClient));

                                    break;
                                case NetworkMessage.MessageType.SYNC:
                                    // Logger.Info("Recevied: " + NetworkMessage.MessageType.SYNC);

                                    //check server version
                                    if (serverMessage.Version == null)
                                    {
                                        Logger.Error("Disconnecting Unversioned Server");
                                        Disconnect();
                                        break;
                                    }

                                    var serverVersion = Version.Parse(serverMessage.Version);
                                    var protocolVersion = Version.Parse(UpdaterChecker.MINIMUM_PROTOCOL_VERSION);

                                    SyncedServerSettings.Instance.ServerVersion = serverMessage.Version;

                                    if (serverVersion < protocolVersion)
                                    {
                                        Logger.Error(
                                            $"Server version ({serverMessage.Version}) older than minimum procotol version ({UpdaterChecker.MINIMUM_PROTOCOL_VERSION}) - disconnecting");

                                        //TODO show warning
                                        //ShowVersionMistmatchWarning(serverMessage.Version);

                                        Disconnect();
                                        break;
                                    }

                                    if (serverMessage.Clients != null)
                                        foreach (var client in serverMessage.Clients)
                                        {
                                            client.LastUpdate = DateTime.Now.Ticks;
                                            //init with LOS true so you can hear them incase of bad DCS install where
                                            //LOS isnt working
                                            client.LineOfSightLoss = 0.0f;
                                            //0.0 is NO LOSS therefore full Line of sight
                                            _clients[client.ClientGuid] = client;

                                            EventBus.Instance.PublishOnUIThreadAsync(
                                                new SRClientUpdateMessage(client));
                                        }

                                    //add server settings
                                    _serverSettings.Decode(serverMessage.ServerSettings);
                                    break;

                                case NetworkMessage.MessageType.SERVER_SETTINGS:

                                    _serverSettings.Decode(serverMessage.ServerSettings);
                                    SyncedServerSettings.Instance.ServerVersion = serverMessage.Version;

                                    //TODO publish update of server settings

                                    //TODO handle EAM
                                    // if (_clientStateSingleton.ExternalAWACSModelSelected &&
                                    //     !_serverSettings.GetSettingAsBool(Common.Setting.ServerSettingsKeys.EXTERNAL_AWACS_MODE))
                                    // {
                                    //     DisconnectExternalAWACSMode();
                                    // }

                                    break;
                                case NetworkMessage.MessageType.CLIENT_DISCONNECT:

                                    SRClientBase outClient;
                                    _clients.TryRemove(serverMessage.Client.ClientGuid, out outClient);

                                    if (outClient != null)
                                        EventBus.Instance.PublishOnUIThreadAsync(
                                            new SRClientUpdateMessage(outClient, false));

                                    break;
                                case NetworkMessage.MessageType.VERSION_MISMATCH:
                                    Logger.Error(
                                        $"Version Mismatch Between Client ({UpdaterChecker.VERSION}) & Server ({serverMessage.Version}) - Disconnecting");

                                    //TODO show warning
                                    //ShowVersionMistmatchWarning(serverMessage.Version);

                                    Disconnect();
                                    break;
                                case NetworkMessage.MessageType.EXTERNAL_AWACS_MODE_PASSWORD:

                                    //TODO handle EAM
                                    // if (serverMessage.Client.Coalition == 0)
                                    // {
                                    //     Logger.Info("External AWACS mode authentication failed");
                                    //
                                    //     CallExternalAWACSModeOnMain(false, 0);
                                    // }
                                    // else if (_radioDCSSync != null && _radioDCSSync.IsListening)
                                    // {
                                    //     Logger.Info("External AWACS mode authentication succeeded, coalition {0}", serverMessage.Client.Coalition == 1 ? "red" : "blue");
                                    //
                                    //     CallExternalAWACSModeOnMain(true, serverMessage.Client.Coalition);
                                    //
                                    //     _radioDCSSync.StartExternalAWACSModeLoop();
                                    // }

                                    break;
                                default:
                                    Logger.Error("Recevied unknown " + line);
                                    break;
                            }
                    }
                    catch (Exception ex)
                    {
                        decodeErrors++;
                        if (!_stop) Logger.Error(ex, "Client exception reading from socket ");

                        if (decodeErrors > MAX_DECODE_ERRORS)
                        {
                            ShowVersionMistmatchWarning("unknown");
                            Disconnect();
                            break;
                        }
                    }

                // do something with line
            }
            catch (Exception ex)
            {
                if (!_stop) Logger.Error(ex, "Client exception reading - Disconnecting ");
            }
        }

        EventBus.Instance.Unsubcribe(this);

        //clear the clients list
        _clients.Clear();

        Disconnect();
    }

    private void HandlePartialUpdate(NetworkMessage networkMessage, SRClientBase client)
    {
        //TODO fix this
        // client.UnitState.Transponder = networkMessage.Client.UnitState.Transponder;
        // client.UnitState.Coalition = networkMessage.Client.UnitState.Coalition;
        // client.UnitState.LatLng = networkMessage.Client.UnitState.LatLng;
        // client.UnitState.Name = networkMessage.Client.UnitState.Name;
        // client.UnitState.UnitId = networkMessage.Client.UnitState.UnitId;
    }

    private void HandleFullUpdate(NetworkMessage networkMessage, SRClientBase client)
    {
        var updatedSrClient = networkMessage.Client;
        //TODO fix this
        // client.UnitState = updatedSrClient.UnitState;
    }

    private void ShowVersionMistmatchWarning(string serverVersion)
    {
        /*
        MessageBox.Show($"The SRS server you're connecting to is incompatible with this Client. " +
                        $"\n\nMake sure to always run the latest version of the SRS Server & Client" +
                        $"\nClient Version: {UpdaterChecker.VERSION}",
                        "SRS Server Incompatible",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
        */
    }

    private void SendToServer(NetworkMessage message)
    {
        try
        {
            _lastSent = DateTime.Now.Ticks;
            message.Version = UpdaterChecker.VERSION;

            var json = message.Encode();

            if (message.MsgType == NetworkMessage.MessageType.RADIO_UPDATE)
                Logger.Debug("Sending Radio Update To Server: " + json);

            var bytes = Encoding.UTF8.GetBytes(json);
            _tcpClient.GetStream().Write(bytes, 0, bytes.Length);
            //Need to flush?
        }
        catch (Exception ex)
        {
            if (!_stop) Logger.Error(ex, "Client exception sending to server");

            Disconnect();
        }
    }

    //implement IDispose? To close stuff properly?
    public void Disconnect()
    {
        EventBus.Instance.Unsubcribe(this);

        _stop = true;

        _lastSent = DateTime.Now.Ticks;

        try
        {
            if (_tcpClient != null)
            {
                _tcpClient?.Close(); // this'll stop the socket blocking
                _tcpClient = null;
                EventBus.Instance.PublishOnUIThreadAsync(new TCPClientStatusMessage(false));
            }
        }
        catch (Exception ex)
        {
        }
        //
        // try
        // {
        //     _udpVoiceHandler?.RequestStop(); // this'll stop the socket blocking
        //     _udpVoiceHandler = null;
        // }
        // catch (Exception ex)
        // {
        // }

        Logger.Error("Disconnecting from server");
    }
}