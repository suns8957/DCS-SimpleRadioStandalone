using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Mobile.Utility;

namespace Ciribob.DCS.SimpleRadio.Standalone.Mobile.Views.Mobile.RadioViews.HandheldRadio;

public partial class HandheldRadioPage : ContentPage
{
    private readonly IDispatcherTimer _updateTimer;

    public HandheldRadioPage()
    {
        InitializeComponent();
        DeviceDisplay.Current.KeepScreenOn = true;

        BindingContext = new RadioViewModel(1);

        _updateTimer = Application.Current.Dispatcher.CreateTimer();
        _updateTimer.Interval = TimeSpan.FromMilliseconds(100);
        _updateTimer.Tick += (s, e) => ((RadioViewModel)BindingContext).RefreshView();
    }

    private void Button_OnPressed(object sender, EventArgs e)
    {
        EventBus.Instance.PublishOnBackgroundThreadAsync(new PTTState { PTTPressed = true });
    }

    private void Button_OnReleased(object sender, EventArgs e)
    {
        EventBus.Instance.PublishOnBackgroundThreadAsync(new PTTState { PTTPressed = false });
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _updateTimer.Start();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _updateTimer.Stop();
    }
}