using System.Windows.Controls;

namespace Ciribob.FS3D.SimpleRadio.Standalone.Client.UI.ClientWindow.ClientSettingsControl;

/// <summary>
///     Interaction logic for ClientSettings.xaml
/// </summary>
public partial class ClientSettings : UserControl
{
    public ClientSettings()
    {
        InitializeComponent();
        DataContext = new ClientSettingsViewModel();
    }
}