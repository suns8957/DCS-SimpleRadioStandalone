using System.Windows;
using System.Windows.Controls;

namespace Ciribob.FS3D.SimpleRadio.Standalone.Client.UI.ClientWindow.ClientSettingsControl;

/// <summary>
///     Interaction logic for BackgroundEffectVolumeControl.xaml
/// </summary>
public partial class BackgroundEffectVolumeControl : UserControl
{
    public static readonly DependencyProperty VolumeSliderDependencyProperty =
        DependencyProperty.Register("VolumeValue", typeof(float), typeof(BackgroundEffectVolumeControl),
            new FrameworkPropertyMetadata((float)0)
        );

    public BackgroundEffectVolumeControl()
    {
        InitializeComponent();
    }

    public float VolumeValue
    {
        set => SetValue(VolumeSliderDependencyProperty, value);
        get
        {
            var val = (float)GetValue(VolumeSliderDependencyProperty);
            return val;
        }
    }
}