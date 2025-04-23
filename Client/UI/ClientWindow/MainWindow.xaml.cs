using System;
using System.ComponentModel;
using System.Runtime;
using System.Windows;
using Ciribob.FS3D.SimpleRadio.Standalone.Client.Settings;
using Ciribob.FS3D.SimpleRadio.Standalone.Common.Settings.Setting;
using MahApps.Metro.Controls;
using NLog;

namespace Ciribob.FS3D.SimpleRadio.Standalone.Client.UI.ClientWindow;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : MetroWindow
{
    private readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;
    private readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public MainWindow()
    {
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

        InitializeComponent();

        // Initialize images/icons
        Images.Init();

        // Initialise sounds
        Sounds.Init();

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = _globalSettings.GetPositionSetting(GlobalSettingsKeys.ClientX).DoubleValue;
        Top = _globalSettings.GetPositionSetting(GlobalSettingsKeys.ClientY).DoubleValue;

        Title = Title + " - " + UpdaterChecker.VERSION;

        if (_globalSettings.GetClientSettingBool(GlobalSettingsKeys.StartMinimised))
        {
            Hide();
            WindowState = WindowState.Minimized;

            Logger.Info("Started FS3D SRS Client " + UpdaterChecker.VERSION + " minimized");
        }
        else
        {
            Logger.Info("Started FS3D SRS Client " + UpdaterChecker.VERSION);
        }

        DataContext = new MainWindowViewModel();

        var args = Environment.GetCommandLineArgs();

        foreach (var arg in args)
            if (arg.StartsWith("--server="))
            {
                var address = arg.Replace("--server=", "");
                ((MainWindowViewModel)DataContext).ServerAddress = address;
                ((MainWindowViewModel)DataContext).Connect();
            }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        ((MainWindowViewModel)DataContext).OnClosing();

        _globalSettings.SetPositionSetting(GlobalSettingsKeys.ClientX, Left);
        _globalSettings.SetPositionSetting(GlobalSettingsKeys.ClientY, Top);

        //save window position
        base.OnClosing(e);
    }

    protected override void OnStateChanged(EventArgs e)
    {
        if (WindowState == WindowState.Minimized &&
            _globalSettings.GetClientSettingBool(GlobalSettingsKeys.MinimiseToTray)) Hide();

        base.OnStateChanged(e);
    }
}