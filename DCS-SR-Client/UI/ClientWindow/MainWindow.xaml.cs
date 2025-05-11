using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Forms;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings.Favourites;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.ClientSettingsControl.Model;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.Favourites;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Utils;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using MahApps.Metro.Controls;
using NLog;
using MessageBox = System.Windows.MessageBox;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : MetroWindow
{
    //
    // private void InitToolTips()
    // {
    //     ExternalAWACSModePassword.ToolTip = ToolTips.ExternalAWACSModePassword;
    //     ExternalAWACSModeName.ToolTip = ToolTips.ExternalAWACSModeName;
    //     ConnectExternalAWACSMode.ToolTip = ToolTips.ExternalAWACSMode;
    // }
    //

    // [MethodImpl(MethodImplOptions.Synchronized)]
    // private void Connect()
    // {
    //     if (ClientState.IsConnected)
    //     {
    //         Stop();
    //     }
    //     else
    //     {
    //         SaveSelectedInputAndOutput();
    //
    //         try
    //         {
    //             //process hostname
    //             var resolvedAddresses = Dns.GetHostAddresses(GetAddressFromTextBox());
    //             var ip = resolvedAddresses.FirstOrDefault(xa =>
    //                 xa.AddressFamily ==
    //                 AddressFamily
    //                     .InterNetwork); // Ensure we get an IPv4 address in case the host resolves to both IPv6 and IPv4
    //
    //             if (ip != null)
    //             {
    //                 _resolvedIp = ip;
    //                 _port = GetPortFromTextBox();
    //
    //                 try
    //                 {
    //                     _client?.Disconnect();
    //                 }
    //                 catch (Exception ex)
    //                 {
    //                 }
    //
    //                 if (_client == null)
    //                     _client = new SRSClientSyncHandler(_guid, UpdateUICallback, delegate(string name, int seat)
    //                     {
    //                         try
    //                         {
    //                             //on MAIN thread
    //                             Application.Current.Dispatcher.Invoke(DispatcherPriority.Background,
    //                                 new ThreadStart(() =>
    //                                 {
    //                                     //Handle Aircraft Name - find matching profile and select if you can
    //                                     name = Regex.Replace(name.Trim().ToLower(), "[^a-zA-Z0-9]", "");
    //                                     //add one to seat so seat_2 is copilot
    //                                     var nameSeat = $"_{seat + 1}";
    //
    //                                     foreach (var profileName in _globalSettings.ProfileSettingsStore
    //                                                  .ProfileNames)
    //                                     {
    //                                         //find matching seat
    //                                         var splitName = profileName.Trim().ToLowerInvariant().Split('_')
    //                                             .First();
    //                                         if (name.StartsWith(Regex.Replace(splitName, "[^a-zA-Z0-9]", "")) &&
    //                                             profileName.Trim().EndsWith(nameSeat))
    //                                         {
    //                                             ControlsProfile.SelectedItem = profileName;
    //                                             return;
    //                                         }
    //                                     }
    //
    //                                     foreach (var profileName in _globalSettings.ProfileSettingsStore
    //                                                  .ProfileNames)
    //                                         //find matching seat
    //                                         if (name.StartsWith(Regex.Replace(profileName.Trim().ToLower(),
    //                                                 "[^a-zA-Z0-9_]", "")))
    //                                         {
    //                                             ControlsProfile.SelectedItem = profileName;
    //                                             return;
    //                                         }
    //
    //                                     ControlsProfile.SelectedIndex = 0;
    //                                 }));
    //                         }
    //                         catch (Exception)
    //                         {
    //                         }
    //                     });
    //
    //                 _client.TryConnect(new IPEndPoint(_resolvedIp, _port), ConnectCallback);
    //
    //                 StartStop.Content = Properties.Resources.StartStopConnecting;
    //                 StartStop.IsEnabled = false;
    //                 Mic.IsEnabled = false;
    //                 Speakers.IsEnabled = false;
    //                 MicOutput.IsEnabled = false;
    //                 Preview.IsEnabled = false;
    //
    //                 if (_audioPreview != null)
    //                 {
    //                     Preview.Content = Properties.Resources.PreviewAudio;
    //                     _audioPreview.StopEncoding();
    //                     _audioPreview = null;
    //                 }
    //             }
    //             else
    //             {
    //                 //invalid ID
    //                 MessageBox.Show(Properties.Resources.MsgBoxInvalidIPText, Properties.Resources.MsgBoxInvalidIP,
    //                     MessageBoxButton.OK,
    //                     MessageBoxImage.Error);
    //
    //                 ClientState.IsConnected = false;
    //                 ToggleServerSettings.IsEnabled = false;
    //             }
    //         }
    //         catch (Exception ex) when (ex is SocketException || ex is ArgumentException)
    //         {
    //             MessageBox.Show(Properties.Resources.MsgBoxInvalidIPText, Properties.Resources.MsgBoxInvalidIP,
    //                 MessageBoxButton.OK,
    //                 MessageBoxImage.Error);
    //
    //             ClientState.IsConnected = false;
    //             ToggleServerSettings.IsEnabled = false;
    //         }
    //     }
    // }
    //
    // private string GetAddressFromTextBox()
    // {
    //     var addr = ServerIp.Text.Trim();
    //
    //     if (addr.Contains(":")) return addr.Split(':')[0];
    //
    //     return addr;
    // }
    //
    // private int GetPortFromTextBox()
    // {
    //     var addr = ServerIp.Text.Trim();
    //
    //     if (addr.Contains(":"))
    //     {
    //         int port;
    //         if (int.TryParse(addr.Split(':')[1], out port)) return port;
    //         throw new ArgumentException("specified port is not valid");
    //     }
    //
    //     return 5002;
    // }
    //
    // private void Stop(bool connectionError = false)
    // {
    //     if (ClientState.IsConnected && _globalSettings.GetClientSettingBool(GlobalSettingsKeys.PlayConnectionSounds))
    //         try
    //         {
    //             Sounds.BeepDisconnected.Play();
    //         }
    //         catch (Exception ex)
    //         {
    //             Logger.Warn(ex, "Failed to play disconnect sound");
    //         }
    //
    //     ClientState.IsConnectionErrored = connectionError;
    //
    //     StartStop.Content = Properties.Resources.StartStop;
    //     StartStop.IsEnabled = true;
    //     Mic.IsEnabled = true;
    //     Speakers.IsEnabled = true;
    //     MicOutput.IsEnabled = true;
    //     Preview.IsEnabled = true;
    //     ClientState.IsConnected = false;
    //     ToggleServerSettings.IsEnabled = false;
    //
    //     ConnectExternalAWACSMode.IsEnabled = false;
    //     ConnectExternalAWACSMode.Content = Properties.Resources.ConnectExternalAWACSMode;
    //
    //     if (!string.IsNullOrWhiteSpace(ClientState.LastSeenName) &&
    //         _globalSettings.GetClientSetting(GlobalSettingsKeys.LastSeenName).StringValue != ClientState.LastSeenName)
    //         _globalSettings.SetClientSetting(GlobalSettingsKeys.LastSeenName, ClientState.LastSeenName);
    //
    //     try
    //     {
    //         _audioManager.StopEncoding();
    //     }
    //     catch (Exception)
    //     {
    //     }
    //
    //     _client?.Disconnect();
    //
    //     ClientState.DcsPlayerRadioInfo.Reset();
    //     ClientState.PlayerCoaltionLocationMetadata.Reset();
    // }
    //
    //

    //
    // private void ConnectCallback(bool result, bool connectionError, string connection)
    // {
    //     var currentConnection = ServerIp.Text.Trim();
    //     if (!currentConnection.Contains(":")) currentConnection += ":5002";
    //
    //     if (result)
    //     {
    //         if (!ClientState.IsConnected)
    //             try
    //             {
    //                 StartStop.Content = Properties.Resources.StartStopDisconnect;
    //                 StartStop.IsEnabled = true;
    //
    //                 ClientState.IsConnected = true;
    //                 ClientState.IsVoipConnected = false;
    //
    //                 if (_globalSettings.GetClientSettingBool(GlobalSettingsKeys.PlayConnectionSounds))
    //                     try
    //                     {
    //                         Sounds.BeepConnected.Play();
    //                     }
    //                     catch (Exception ex)
    //                     {
    //                         Logger.Warn(ex, "Failed to play connect sound");
    //                     }
    //
    //                 _globalSettings.SetClientSetting(GlobalSettingsKeys.LastServer, ServerIp.Text);
    //
    //                 _audioManager.StartEncoding(_guid, InputManager,
    //                     new IPEndPoint(_resolvedIp, _port));
    //             }
    //             catch (Exception ex)
    //             {
    //                 Logger.Error(ex,
    //                     "Unable to get audio device - likely output device error - Pick another. Error:" +
    //                     ex.Message);
    //                 Stop();
    //
    //                 var messageBoxResult = CustomMessageBox.ShowYesNo(
    //                     Properties.Resources.MsgBoxAudioErrorText,
    //                     Properties.Resources.MsgBoxAudioError,
    //                     "OPEN PRIVACY SETTINGS",
    //                     "JOIN DISCORD SERVER",
    //                     MessageBoxImage.Error);
    //
    //                 if (messageBoxResult == MessageBoxResult.Yes) Process.Start("https://discord.gg/baw7g3t");
    //             }
    //     }
    //     else if (string.Equals(currentConnection, connection, StringComparison.OrdinalIgnoreCase))
    //     {
    //         // Only stop connection/reset state if connection is currently active
    //         // Autoconnect mismatch will quickly disconnect/reconnect, leading to double-callbacks
    //         Stop(connectionError);
    //     }
    //     else
    //     {
    //         if (!ClientState.IsConnected) Stop(connectionError);
    //     }
    // }
    //


    //
    // private void AutoConnect(string address, int port)
    // {
    //     var connection = $"{address}:{port}";
    //
    //     Logger.Info($"Received AutoConnect DCS-SRS @ {connection}");
    //
    //     var enabled = _globalSettings.GetClientSetting(GlobalSettingsKeys.AutoConnect).BoolValue;
    //
    //     if (!enabled)
    //     {
    //         Logger.Info("Ignored Autoconnect - not Enabled");
    //         return;
    //     }
    //
    //     if (ClientState.IsConnected)
    //     {
    //         // Always show prompt about active/advertised SRS connection mismatch if client is already connected
    //         var currentConnectionParts = ServerIp.Text.Trim().Split(':');
    //         var currentAddress = currentConnectionParts[0];
    //         var currentPort = 5002;
    //         if (currentConnectionParts.Length >= 2)
    //             if (!int.TryParse(currentConnectionParts[1], out currentPort))
    //             {
    //                 Logger.Warn(
    //                     $"Failed to parse port {currentConnectionParts[1]} of current connection, falling back to 5002 for autoconnect comparison");
    //                 currentPort = 5002;
    //             }
    //
    //         var currentConnection = $"{currentAddress}:{currentPort}";
    //
    //         if (string.Equals(address, currentAddress, StringComparison.OrdinalIgnoreCase) && port == currentPort)
    //         {
    //             // Current connection matches SRS server advertised by DCS, all good
    //             Logger.Info(
    //                 $"Current SRS connection {currentConnection} matches advertised server {connection}, ignoring autoconnect");
    //             return;
    //         }
    //
    //         if (port != currentPort)
    //         {
    //             // Port mismatch, will always be a different server, no need to perform hostname lookups
    //             HandleAutoConnectMismatch(currentConnection, connection);
    //             return;
    //         }
    //
    //         // Perform DNS lookup of advertised and current hostnames to find hostname/resolved IP matches
    //         var currentIPs = new List<string>();
    //
    //         if (IPAddress.TryParse(currentAddress, out var currentIP))
    //             currentIPs.Add(currentIP.ToString());
    //         else
    //             try
    //             {
    //                 foreach (var ip in Dns.GetHostAddresses(currentConnectionParts[0]))
    //                     // SRS currently only supports IPv4 (due to address/port parsing)
    //                     if (ip.AddressFamily == AddressFamily.InterNetwork)
    //                         currentIPs.Add(ip.ToString());
    //             }
    //             catch (Exception e)
    //             {
    //                 Logger.Warn(e,
    //                     $"Failed to resolve current SRS host {currentConnectionParts[0]} to IP addresses, ignoring autoconnect advertisement");
    //             }
    //
    //         if (currentIPs.Count == 0)
    //         {
    //             Logger.Warn(
    //                 $"Failed to resolve current SRS host {currentConnectionParts[0]} to IP addresses, ignoring autoconnect advertisement");
    //             return;
    //         }
    //
    //         var advertisedIPs = new List<string>();
    //
    //         if (IPAddress.TryParse(address, out var advertisedIP))
    //             advertisedIPs.Add(advertisedIP.ToString());
    //         else
    //             try
    //             {
    //                 foreach (var ip in Dns.GetHostAddresses(connection))
    //                     // SRS currently only supports IPv4 (due to address/port parsing)
    //                     if (ip.AddressFamily == AddressFamily.InterNetwork)
    //                         advertisedIPs.Add(ip.ToString());
    //             }
    //             catch (Exception e)
    //             {
    //                 Logger.Warn(e,
    //                     $"Failed to resolve advertised SRS host {address} to IP addresses, ignoring autoconnect advertisement");
    //                 return;
    //             }
    //
    //         if (!currentIPs.Intersect(advertisedIPs).Any())
    //             // No resolved IPs match, display mismatch warning
    //             HandleAutoConnectMismatch(currentConnection, connection);
    //     }
    //     else
    //     {
    //         // Show auto connect prompt if client is not connected yet and setting has been enabled, otherwise automatically connect
    //         var showPrompt = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.AutoConnectPrompt);
    //
    //         var connectToServer = !showPrompt;
    //         if (_globalSettings.GetClientSettingBool(GlobalSettingsKeys.AutoConnectPrompt))
    //         {
    //             WindowHelper.BringProcessToFront(Process.GetCurrentProcess());
    //
    //             var result = MessageBox.Show(this,
    //                 $"{Properties.Resources.MsgBoxAutoConnectText} {address}:{port}? ", "Auto Connect",
    //                 MessageBoxButton.YesNo,
    //                 MessageBoxImage.Question);
    //
    //             connectToServer = result == MessageBoxResult.Yes && StartStop.Content.ToString().ToLower() == "connect";
    //         }
    //
    //         if (connectToServer)
    //         {
    //             ServerIp.Text = connection;
    //             Connect();
    //         }
    //     }
    // }
    //
    // private async void HandleAutoConnectMismatch(string currentConnection, string advertisedConnection)
    // {
    //     // Show auto connect mismatch prompt if setting has been enabled (default), otherwise automatically switch server
    //     var showPrompt = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.AutoConnectMismatchPrompt);
    //
    //     Logger.Info(
    //         $"Current SRS connection {currentConnection} does not match advertised server {advertisedConnection}, {(showPrompt ? "displaying mismatch prompt" : "automatically switching server")}");
    //
    //     var switchServer = !showPrompt;
    //     if (showPrompt)
    //     {
    //         WindowHelper.BringProcessToFront(Process.GetCurrentProcess());
    //
    //         var result = MessageBox.Show(this,
    //             $"{Properties.Resources.MsgBoxMismatchText1} {advertisedConnection} {Properties.Resources.MsgBoxMismatchText2} {currentConnection} {Properties.Resources.MsgBoxMismatchText3}\n\n" +
    //             $"{Properties.Resources.MsgBoxMismatchText4}",
    //             Properties.Resources.MsgBoxMismatch,
    //             MessageBoxButton.YesNo,
    //             MessageBoxImage.Warning);
    //
    //         switchServer = result == MessageBoxResult.Yes;
    //     }
    //
    //     if (switchServer)
    //     {
    //         Stop();
    //
    //         StartStop.IsEnabled = false;
    //         StartStop.Content = Properties.Resources.StartStopConnecting;
    //         await Task.Delay(2000);
    //         StartStop.IsEnabled = true;
    //         ServerIp.Text = advertisedConnection;
    //         Connect();
    //     }
    // }

    //
    // private void ConnectExternalAWACSMode_OnClick(object sender, RoutedEventArgs e)
    // {
    //     if (_client == null ||
    //         !ClientState.IsConnected ||
    //         !_serverSettings.GetSettingAsBool(ServerSettingsKeys.EXTERNAL_AWACS_MODE) ||
    //         (!ClientState.ExternalAWACSModelSelected &&
    //          string.IsNullOrWhiteSpace(ExternalAWACSModePassword.Password)))
    //         return;
    //
    //     // Already connected, disconnect
    //     if (ClientState.ExternalAWACSModelSelected)
    //     {
    //         _client.DisconnectExternalAWACSMode();
    //     }
    //     else if (!ClientState.IsGameExportConnected) //only if we're not in game
    //     {
    //         ClientState.LastSeenName = ExternalAWACSModeName.Text;
    //         _client.ConnectExternalAWACSMode(ExternalAWACSModePassword.Password.Trim(),
    //             ExternalAWACSModeConnectionChanged);
    //     }
    // }
    //
    // private void ExternalAWACSModeConnectionChanged(bool result, int coalition)
    // {
    //     if (result)
    //     {
    //         ClientState.ExternalAWACSModelSelected = true;
    //         ClientState.PlayerCoaltionLocationMetadata.side = coalition;
    //         ClientState.PlayerCoaltionLocationMetadata.name = ClientState.LastSeenName;
    //         ClientState.DcsPlayerRadioInfo.name = ClientState.LastSeenName;
    //
    //         ConnectExternalAWACSMode.Content = Properties.Resources.DisconnectExternalAWACSMode;
    //     }
    //     else
    //     {
    //         ClientState.ExternalAWACSModelSelected = false;
    //         ClientState.PlayerCoaltionLocationMetadata.side = 0;
    //         ClientState.PlayerCoaltionLocationMetadata.name = "";
    //         ClientState.DcsPlayerRadioInfo.name = "";
    //         ClientState.DcsPlayerRadioInfo.LastUpdate = 0;
    //         ClientState.LastSent = 0;
    //
    //         ConnectExternalAWACSMode.Content = Properties.Resources.ConnectExternalAWACSMode;
    //         ExternalAWACSModePassword.IsEnabled =
    //             _serverSettings.GetSettingAsBool(ServerSettingsKeys.EXTERNAL_AWACS_MODE);
    //         ExternalAWACSModeName.IsEnabled =
    //             _serverSettings.GetSettingAsBool(ServerSettingsKeys.EXTERNAL_AWACS_MODE);
    //     }
    // }
    //


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

            Logger.Info("Started DCS SRS Client " + UpdaterChecker.VERSION + " minimized");
        }
        else
        {
            Logger.Info("Started DCS SRS Client " + UpdaterChecker.VERSION);
        }

        DataContext = new MainWindowViewModel()
        {
            FavouriteServersViewModel = new FavouriteServersViewModel(new CsvFavouriteServerStore())
        };

        var args = Environment.GetCommandLineArgs();

        foreach (var arg in args)
            if (arg.StartsWith("--server="))
            {
                var address = arg.Replace("--server=", "");
                ((MainWindowViewModel)DataContext).ServerAddress = address;
                ((MainWindowViewModel)DataContext).Connect();
            }

        FavouriteServersView.DataContext = ((MainWindowViewModel)DataContext).FavouriteServersViewModel;

        //TODO make this a singleton with a callback to check for updates
        UpdaterChecker.Instance.CheckForUpdate(
            _globalSettings.GetClientSettingBool(GlobalSettingsKeys.CheckForBetaUpdates),
            result =>
            {
                //TODO fix resources - it should work!
                // if (result.UpdateAvailable)
                // {
                //         var choice = MessageBox.Show($"{Properties.Resources.MsgBoxUpdate1} {result.Branch} {Properties.Resources.MsgBoxUpdate2} {result.Branch} {Properties.Resources.MsgBoxUpdate3}\n\n{Properties.Resources.MsgBoxUpdate4}",
                //             Properties.Resources.MsgBoxUpdateTitle, MessageBoxButton.YesNoCancel, MessageBoxImage.Information);
                //
                //         if (choice == MessageBoxResult.Yes)
                //         {
                //             try
                //             {
                //                 UpdaterChecker.Instance.LaunchUpdater(result.Beta);
                //             }
                //             catch (Exception)
                //             {
                //                 MessageBox.Show($"{Properties.Resources.MsgBoxUpdateFailed}",
                //                     Properties.Resources.MsgBoxUpdateFailedTitle, MessageBoxButton.YesNoCancel, MessageBoxImage.Information);
                //
                //                 Process.Start( new ProcessStartInfo(result.Url)
                //                     { UseShellExecute = true });
                //             }
                //
                //         }
                //         else if (choice == MessageBoxResult.No)
                //         {
                //             Process.Start( new ProcessStartInfo(result.Url)
                //                 { UseShellExecute = true });
                //            
                //         }
                //     
                // }
            });


        //TODO move this
        UpdatePresetsFolderLabel();

        InitFlowDocument();

        CheckWindowVisibility();
    }

    private void CheckWindowVisibility()
    {
        if (_globalSettings.GetClientSettingBool(GlobalSettingsKeys.DisableWindowVisibilityCheck))
        {
            Logger.Info("Window visibility check is disabled, skipping");
            return;
        }

        var mainWindowVisible = false;
        var radioWindowVisible = false;
        var awacsWindowVisible = false;

        var mainWindowX = (int)_globalSettings.GetPositionSetting(GlobalSettingsKeys.ClientX).DoubleValue;
        var mainWindowY = (int)_globalSettings.GetPositionSetting(GlobalSettingsKeys.ClientY).DoubleValue;
        var radioWindowX = (int)_globalSettings.GetPositionSetting(GlobalSettingsKeys.RadioX).DoubleValue;
        var radioWindowY = (int)_globalSettings.GetPositionSetting(GlobalSettingsKeys.RadioY).DoubleValue;
        var awacsWindowX = (int)_globalSettings.GetPositionSetting(GlobalSettingsKeys.AwacsX).DoubleValue;
        var awacsWindowY = (int)_globalSettings.GetPositionSetting(GlobalSettingsKeys.AwacsY).DoubleValue;

        Logger.Info($"Checking window visibility for main client window {{X={mainWindowX},Y={mainWindowY}}}");
        Logger.Info($"Checking window visibility for radio overlay {{X={radioWindowX},Y={radioWindowY}}}");
        Logger.Info($"Checking window visibility for AWACS overlay {{X={awacsWindowX},Y={awacsWindowY}}}");

        foreach (var screen in Screen.AllScreens)
        {
            Logger.Info(
                $"Checking {(screen.Primary ? "primary " : "")}screen {screen.DeviceName} with bounds {screen.Bounds} for window visibility");

            if (screen.Bounds.Contains(mainWindowX, mainWindowY))
            {
                Logger.Info(
                    $"Main client window {{X={mainWindowX},Y={mainWindowY}}} is visible on {(screen.Primary ? "primary " : "")}screen {screen.DeviceName} with bounds {screen.Bounds}");
                mainWindowVisible = true;
            }

            if (screen.Bounds.Contains(radioWindowX, radioWindowY))
            {
                Logger.Info(
                    $"Radio overlay {{X={radioWindowX},Y={radioWindowY}}} is visible on {(screen.Primary ? "primary " : "")}screen {screen.DeviceName} with bounds {screen.Bounds}");
                radioWindowVisible = true;
            }

            if (screen.Bounds.Contains(awacsWindowX, awacsWindowY))
            {
                Logger.Info(
                    $"AWACS overlay {{X={awacsWindowX},Y={awacsWindowY}}} is visible on {(screen.Primary ? "primary " : "")}screen {screen.DeviceName} with bounds {screen.Bounds}");
                awacsWindowVisible = true;
            }
        }

        if (!mainWindowVisible)
        {
            MessageBox.Show(this,
                Properties.Resources.MsgBoxNotVisibleText,
                Properties.Resources.MsgBoxNotVisible,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            Logger.Warn(
                $"Main client window outside visible area of monitors, resetting position ({mainWindowX},{mainWindowY}) to defaults");

            _globalSettings.SetPositionSetting(GlobalSettingsKeys.ClientX, 200);
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.ClientY, 200);

            Left = 200;
            Top = 200;
        }

        if (!radioWindowVisible)
        {
            MessageBox.Show(this,
                Properties.Resources.MsgBoxNotVisibleText,
                Properties.Resources.MsgBoxNotVisible,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            Logger.Warn(
                $"Radio overlay window outside visible area of monitors, resetting position ({radioWindowX},{radioWindowY}) to defaults");

            EventBus.Instance.PublishOnUIThreadAsync(new CloseRadioOverlayMessage());

            _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioX, 300);
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioY, 300);
        }

        if (!awacsWindowVisible)
        {
            MessageBox.Show(this,
                Properties.Resources.MsgBoxNotVisibleText,
                Properties.Resources.MsgBoxNotVisible,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            Logger.Warn(
                $"AWACS overlay window outside visible area of monitors, resetting position ({awacsWindowX},{awacsWindowY}) to defaults");

            _globalSettings.SetPositionSetting(GlobalSettingsKeys.AwacsX, 300);
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.AwacsY, 300);
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

    private void LaunchAddressTab(object sender, RoutedEventArgs e)
    {
        TabControl.SelectedItem = FavouritesSeversTab;
    }

    private void UpdatePresetsFolderLabel()
    {
        var presetsFolder = _globalSettings.GetClientSetting(GlobalSettingsKeys.LastPresetsFolder).RawValue;
        if (!string.IsNullOrWhiteSpace(presetsFolder))
        {
            PresetsFolderLabel.Content = Path.GetFileName(presetsFolder);
            PresetsFolderLabel.ToolTip = presetsFolder;
        }
        else
        {
            PresetsFolderLabel.Content = "(default)";
            PresetsFolderLabel.ToolTip = Directory.GetCurrentDirectory();
        }
    }

    private void PresetsFolderBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var selectPresetsFolder = new FolderBrowserDialog();
        selectPresetsFolder.SelectedPath = PresetsFolderLabel.ToolTip.ToString();
        if (selectPresetsFolder.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.LastPresetsFolder, selectPresetsFolder.SelectedPath);
            UpdatePresetsFolderLabel();
        }
    }

    private void PresetsFolderResetButton_Click(object sender, RoutedEventArgs e)
    {
        _globalSettings.SetClientSetting(GlobalSettingsKeys.LastPresetsFolder, string.Empty);
        UpdatePresetsFolderLabel();
    }

    private void InitFlowDocument()
    {
        //make hyperlinks work
        var hyperlinks = WPFElementHelper.GetVisuals(AboutFlowDocument).OfType<Hyperlink>();
        foreach (var link in hyperlinks)
            link.RequestNavigate += (sender, args) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo(args.Uri.AbsoluteUri)
                        { UseShellExecute = true });
                }
                catch (Exception)
                {
                    // ignored
                }

                args.Handled = true;
            };
    }
}