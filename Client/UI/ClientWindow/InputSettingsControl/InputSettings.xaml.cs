using System.Windows;
using System.Windows.Controls;
using Ciribob.FS3D.SimpleRadio.Standalone.Client.Singletons;

namespace Ciribob.FS3D.SimpleRadio.Standalone.Client.UI.ClientWindow.InputSettingsControl;

/// <summary>
///     Interaction logic for InputSettings.xaml
/// </summary>
public partial class InputSettings : UserControl
{
    public InputSettings()
    {
        InitializeComponent();
    }

    private void Rescan_OnClick(object sender, RoutedEventArgs e)
    {
        InputDeviceManager.Instance.InitDevices();
    }
}