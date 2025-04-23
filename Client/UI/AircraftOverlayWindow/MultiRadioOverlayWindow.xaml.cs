using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Ciribob.FS3D.SimpleRadio.Standalone.Client.Settings;
using Ciribob.FS3D.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.FS3D.SimpleRadio.Standalone.Client.UI.HandheldRadioOverlayWindow;
using NLog;

namespace Ciribob.FS3D.SimpleRadio.Standalone.Client.UI.AircraftOverlayWindow;

/// <summary>
///     Interaction logic for RadioOverlayWindow.xaml
/// </summary>
public partial class MultiRadioOverlayWindow : Window
{
    private readonly double _aspectRatio;


    private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;

    private readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;

    private readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly RadioControlGroup[] radioControlGroup = new RadioControlGroup[10];

    public MultiRadioOverlayWindow()
    {
        //switch to awacs
        //TODO on loading the overlay
        //load the aircraft-radio.json
        //load the fixed channel files
        //send the new radio channel config (use the singleton to co-ordinate)
        //on closing the overlay, set all the radios to disabled
        //close the overlay if the server is not connected? how to keep in sync with the server?

        _clientStateSingleton.PlayerUnitState.LoadMultiRadio();


        InitializeComponent();

        DataContext = new MultiRadioOverlayWindowViewModel();

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = _globalSettings.GetPositionSetting(GlobalSettingsKeys.AwacsX).DoubleValue;
        Top = _globalSettings.GetPositionSetting(GlobalSettingsKeys.AwacsY).DoubleValue;

        _aspectRatio = MinWidth / MinHeight;

        AllowsTransparency = true;
        windowOpacitySlider.Value = Opacity;

        radioControlGroup[0] = radio1;
        radioControlGroup[1] = radio2;
        radioControlGroup[2] = radio3;
        radioControlGroup[3] = radio4;
        radioControlGroup[4] = radio5;
        radioControlGroup[5] = radio6;
        radioControlGroup[6] = radio7;
        radioControlGroup[7] = radio8;
        radioControlGroup[8] = radio9;
        radioControlGroup[9] = radio10;

        for (var i = 0; i < radioControlGroup.Length; i++)
        {
            var dataContext = new HandheldRadioOverlayViewModel(i + 1);
            radioControlGroup[i].DataContext = dataContext;
            dataContext.Start();
        }

        CalculateScale();

        ((MultiRadioOverlayWindowViewModel)DataContext).Start();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _globalSettings.SetPositionSetting(GlobalSettingsKeys.AwacsX, Left);
        _globalSettings.SetPositionSetting(GlobalSettingsKeys.AwacsY, Top);

        base.OnClosing(e);

        for (var i = 0; i < radioControlGroup.Length; i++)
        {
            var dataContext = (HandheldRadioOverlayViewModel)radioControlGroup[i].DataContext;
            dataContext?.Stop();
        }

        ((MultiRadioOverlayWindowViewModel)DataContext).Stop();
    }

    private void Button_Minimise(object sender, RoutedEventArgs e)
    {
        // Minimising a window without a taskbar icon leads to the window's menu bar still showing up in the bottom of screen
        // Since controls are unusable, but a very small portion of the always-on-top window still showing, we're closing it instead, similar to toggling the overlay
        if (_globalSettings.GetClientSettingBool(GlobalSettingsKeys.RadioOverlayTaskbarHide))
            Close();
        else
            WindowState = WindowState.Minimized;
    }


    private void Button_Close(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void windowOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        Opacity = e.NewValue;
    }

    private void containerPanel_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        //force aspect ratio
        CalculateScale();

        WindowState = WindowState.Normal;
    }

    private void CalculateScale()
    {
        var yScale = ActualHeight / RadioOverlayWin.MinHeight;
        var xScale = ActualWidth / RadioOverlayWin.MinWidth;
        var value = Math.Max(xScale, yScale);
        ScaleValue = (double)OnCoerceScaleValue(RadioOverlayWin, value);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        if (sizeInfo.WidthChanged)
            Width = sizeInfo.NewSize.Height * _aspectRatio;
        else
            Height = sizeInfo.NewSize.Width / _aspectRatio;
    }

    private void AircraftOverlayWindow_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
        catch (Exception ex)
        {
            //can throw an error if its somehow caused when the left mouse button isnt down
        }
    }


    #region ScaleValue Depdency Property //StackOverflow: http://stackoverflow.com/questions/3193339/tips-on-developing-resolution-independent-application/5000120#5000120

    public static readonly DependencyProperty ScaleValueProperty = DependencyProperty.Register("ScaleValue",
        typeof(double), typeof(MultiRadioOverlayWindow),
        new UIPropertyMetadata(1.0, OnScaleValueChanged,
            OnCoerceScaleValue));


    private static object OnCoerceScaleValue(DependencyObject o, object value)
    {
        var mainWindow = o as MultiRadioOverlayWindow;
        if (mainWindow != null)
            return mainWindow.OnCoerceScaleValue((double)value);
        return value;
    }

    private static void OnScaleValueChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
    {
        var mainWindow = o as MultiRadioOverlayWindow;
        mainWindow?.OnScaleValueChanged((double)e.OldValue, (double)e.NewValue);
    }

    protected virtual double OnCoerceScaleValue(double value)
    {
        if (double.IsNaN(value))
            return 1.0f;

        value = Math.Max(0.1, value);
        return value;
    }

    protected virtual void OnScaleValueChanged(double oldValue, double newValue)
    {
    }

    public double ScaleValue
    {
        get => (double)GetValue(ScaleValueProperty);
        set => SetValue(ScaleValueProperty, value);
    }

    #endregion
}