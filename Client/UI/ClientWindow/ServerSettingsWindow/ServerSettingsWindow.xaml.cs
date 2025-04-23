using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using Ciribob.FS3D.SimpleRadio.Standalone.Common.Settings.Setting;
using Ciribob.SRS.Common.Network.Singletons;
using MahApps.Metro.Controls;
using NLog;

namespace Ciribob.FS3D.SimpleRadio.Standalone.Client.UI.ClientWindow.ServerSettingsWindow;

/// <summary>
///     Interaction logic for ServerSettingsWindow.xaml
/// </summary>
public partial class ServerSettingsWindow : MetroWindow
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly SyncedServerSettings _serverSettings = SyncedServerSettings.Instance;
    private readonly DispatcherTimer _updateTimer;

    public ServerSettingsWindow()
    {
        InitializeComponent();

        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _updateTimer.Tick += UpdateUI;
        _updateTimer.Start();

        UpdateUI(null, null);
    }

    private void UpdateUI(object sender, EventArgs e)
    {
        var settings = _serverSettings;

        try
        {
            RealRadio.Content = settings.GetSettingAsBool(ServerSettingsKeys.IRL_RADIO_TX) ? "ON" : "OFF";
            ServerVersion.Content = SyncedServerSettings.Instance.ServerVersion;
        }
        catch (IndexOutOfRangeException ex)
        {
            Logger.Warn("Missing Server Option - Connected to old server");
        }
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        _updateTimer.Stop();
    }
}