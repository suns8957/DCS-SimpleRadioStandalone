using System.Windows;
using System.Windows.Controls;

namespace Ciribob.FS3D.SimpleRadio.Standalone.Client.UI.ClientWindow.ClientSettingsControl;

/// <summary>
///     Interaction logic for SettingsToggleControl.xaml
/// </summary>
public partial class SettingsToggleControl : UserControl
{
    public static readonly DependencyProperty ToggleDependencyProperty =
        DependencyProperty.Register("ToggleValue", typeof(bool), typeof(SettingsToggleControl),
            new FrameworkPropertyMetadata(false)
        );

    public SettingsToggleControl()
    {
        /*
         *
         * <!-- <ToggleButton.Style> -->
           <!--     <Style TargetType="{x:Type ToggleButton}"> -->
           <!--         <Setter Property="Content" Value="ON" /> -->
           <!--         <Style.Triggers> -->
           <!--             <Trigger Property="IsChecked" Value="True"> -->
           <!--                 <Setter Property="Content" Value="ON" /> -->
           <!--             </Trigger> -->
           <!--             <Trigger Property="IsChecked" Value="False"> -->
           <!--                 <Setter Property="Content" Value="OFF" /> -->
           <!--             </Trigger> -->
           <!--         </Style.Triggers> -->
           <!--     </Style> -->
           <!-- </ToggleButton.Style> -->
         *
         */

        // DataTrigger tg = new DataTrigger()
        // {
        //     Binding = new Binding("IsChecked"),
        //     Value = true
        // };
        //
        // tg.Setters.Add(new Setter()
        // {
        //     Property = ToggleButton.ContentProperty,
        //     Value = "ON"
        // });
        //
        // DataTrigger tg2 = new DataTrigger()
        // {
        //     Binding = new Binding("IsChecked"),
        //     Value = false
        // };
        //
        // tg2.Setters.Add(new Setter()
        // {
        //     Property = ToggleButton.ContentProperty,
        //     Value = "OFF"
        // });
        //
        // Toggle.Style.Triggers.Add(tg);

        InitializeComponent();

        Toggle.Checked += (sender, args) => Toggle.Content = "ON";
        Toggle.Unchecked += (sender, args) => Toggle.Content = "OFF";
    }


    public bool ToggleValue
    {
        set => SetValue(ToggleDependencyProperty, value);
        get
        {
            var val = (bool)GetValue(ToggleDependencyProperty);
            return val;
        }
    }
}