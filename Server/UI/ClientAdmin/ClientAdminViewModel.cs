using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Models;
using NLog;
using LogManager = NLog.LogManager;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.UI.ClientAdmin;

public sealed class ClientAdminViewModel : Screen, IHandle<ServerStateMessage>
{
    private static readonly TimeSpan LastTransmissionThreshold = TimeSpan.FromMilliseconds(200);

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly IEventAggregator _eventAggregator;
    private readonly DispatcherTimer _updateTimer;

    public ClientAdminViewModel(IEventAggregator eventAggregator)
    {
        _eventAggregator = eventAggregator;
        _eventAggregator.Subscribe(this);

        DisplayName = "SR Client List";

        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _updateTimer.Tick += _updateTimer_Tick;
    }

    public ObservableCollection<ClientViewModel> Clients { get; } = new();

    public async Task HandleAsync(ServerStateMessage message, CancellationToken token)
    {
        Clients.Clear();

        message.Clients.Apply(client => Clients.Add(new ClientViewModel(client, _eventAggregator)));
    }

    protected override async Task OnActivateAsync(CancellationToken token)
    {
        _updateTimer?.Start();

        base.OnActivateAsync(token);
    }

    protected override async Task OnDeactivateAsync(bool close, CancellationToken token)
    {
        if (close) _updateTimer?.Stop();

        base.OnDeactivateAsync(close, token);
    }

    private void _updateTimer_Tick(object sender, EventArgs e)
    {
        foreach (var client in Clients)
            if (DateTime.Now - client.Client.LastTransmissionReceived >= LastTransmissionThreshold)
                client.Client.TransmittingFrequency = "---";
    }
}