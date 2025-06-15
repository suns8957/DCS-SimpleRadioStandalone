using System;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using Newtonsoft.Json;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Server;

public class HttpServer
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly ConcurrentDictionary<string, SRClientBase> _connectedClients;
    private readonly bool _enabled;
    private readonly int _port = 8080;
    private readonly ServerState _serverState;

    private HttpListener _listener;

    public HttpServer(ConcurrentDictionary<string, SRClientBase> connectedClients, ServerState serverState)
    {
        _connectedClients = connectedClients;
        _serverState = serverState;
        _port = ServerSettingsStore.Instance.GetServerSetting(ServerSettingsKeys.HTTP_SERVER_PORT).IntValue;
        _enabled = ServerSettingsStore.Instance.GetServerSetting(ServerSettingsKeys.HTTP_SERVER_ENABLED).BoolValue;
    }

    public void Start()
    {
        if (_enabled)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://*:" + _port + "/");
            _listener.Start();
            Logger.Info("HTTP Server Started on Port " + _port);
            Receive();
        }
        else
        {
            Logger.Info("HTTP Server DISABLED on PORT " + _port);
        }
    }

    public void Stop()
    {
        if (_enabled)
        {
            Logger.Info("HTTP Server Stopped on Port " + _port);
            _listener.Stop();
        }
    }

    private void Receive()
    {
        _listener.BeginGetContext(ListenerCallback, _listener);
    }

    private void ListenerCallback(IAsyncResult result)
    {
        if (_listener.IsListening)
        {
            var context = _listener.EndGetContext(result);

            try
            {
                HandleRequest(context);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error handling HTTP Request");
                try
                {
                    context.Response.StatusCode = 500;
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            try
            {
                context.Response.Close();
            }
            catch (Exception)
            {
                // ignored
            }

            Receive();
        }
    }

    private void HandleRequest(HttpListenerContext context)
    {
        Logger.Info(
            $"HTTP Request {context?.Request?.Url} {context?.Request?.HttpMethod} from {context?.Request?.RemoteEndPoint}");

        if (context.Request.Url == null)
            return;

        if (context.Request.HttpMethod == "GET" && context.Request.Url != null &&
            context.Request.Url.AbsolutePath == "/clients")
        {
            var data = new ClientListExport
                { Clients = _connectedClients.Values, ServerVersion = UpdaterChecker.VERSION };
            var json = JsonConvert.SerializeObject(data) + "\n";

            var output = context.Response.OutputStream;
            using (output)
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                var buffer = Encoding.UTF8.GetBytes(json);
                output.Write(buffer, 0, buffer.Length);
                output.Flush();
            }
        }
        else if (context.Request.HttpMethod == "POST")
        {
            if (context.Request.Url.AbsolutePath.StartsWith("/client/ban/guid/"))
            {
                var clientGuid = context.Request.Url.AbsolutePath.Replace("/client/ban/guid", "");

                if (_connectedClients.TryGetValue(clientGuid, out var client))
                    _serverState.WriteBanIP(client);
                else
                    context.Response.StatusCode = 404;
            }
            else if (context.Request.Url.AbsolutePath.StartsWith("/client/ban/name/"))
            {
                var clientName = context.Request.Url.AbsolutePath.Replace("/client/ban/name/", "").Trim()
                    .ToLowerInvariant();

                foreach (var client in _connectedClients)
                    if (client.Value.Name.Trim().ToLowerInvariant() == clientName)
                    {
                        _serverState.WriteBanIP(client.Value);
                        context.Response.StatusCode = 200;
                        return;
                    }

                context.Response.StatusCode = 404;
            }
            else if (context.Request.Url.AbsolutePath.StartsWith("/client/kick/guid/"))
            {
                var clientGuid = context.Request.Url.AbsolutePath.Replace("/client/kick/guid/", "");

                if (_connectedClients.TryGetValue(clientGuid, out var client))
                {
                    _serverState.KickClient(client);
                    context.Response.StatusCode = 200;
                }
                else
                {
                    context.Response.StatusCode = 404;
                }
            }
            else if (context.Request.Url.AbsolutePath.StartsWith("/client/kick/name/"))
            {
                var clientName = context.Request.Url.AbsolutePath.Replace("/client/kick/name/", "").Trim()
                    .ToLowerInvariant();

                foreach (var client in _connectedClients)
                    if (client.Value.Name.Trim().ToLowerInvariant() == clientName)
                    {
                        _serverState.KickClient(client.Value);
                        context.Response.StatusCode = 200;
                        return;
                    }

                context.Response.StatusCode = 404;
            }
            else
            {
                context.Response.StatusCode = 404;
            }
        }
        else
        {
            context.Response.StatusCode = 404;
        }
    }
}