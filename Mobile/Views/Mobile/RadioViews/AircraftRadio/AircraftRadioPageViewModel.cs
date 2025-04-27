using Ciribob.DCS.SimpleRadio.Standalone.Mobile.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Mobile.Singleton;
using Ciribob.DCS.SimpleRadio.Standalone.Mobile.Utility;

namespace Ciribob.DCS.SimpleRadio.Standalone.Mobile.Views.Mobile.RadioViews.AircraftRadio;

public class AircraftRadioPageViewModel : PropertyChangedBase
{
    private static readonly Color RadioActiveTransmit = Color.Parse("#96FF6D");
    private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;

    public AircraftRadioPageViewModel()
    {
        RadioSelect = new Command(() => { RadioHelper.SelectRadio(0); }, () => IsAvailable);
        HotIntercomMicToggle = new Command(() =>
        {
            ClientStateSingleton.Instance.PlayerUnitState.IntercomHotMic =
                !ClientStateSingleton.Instance.PlayerUnitState.IntercomHotMic;
            NotifyPropertyChanged(nameof(HotIntercomTextColour));
        }, () => IsAvailable);
    }

    public bool IsAvailable
    {
        get
        {
            var radio = Radio;

            if (radio == null) return false;

            return radio.Modulation != Modulation.DISABLED;
        }
    }

    public Radio Radio => ClientStateSingleton.Instance.PlayerUnitState.Radios[0];

    public Command RadioSelect { get; set; }

    public Color RadioActiveFill
    {
        get
        {
            if (Radio == null || !IsAvailable) return Colors.Red;

            if (ClientStateSingleton.Instance.PlayerUnitState.SelectedRadio != 0)
                return Colors.Orange;

            if (ClientStateSingleton.Instance.RadioSendingState.IsSending &&
                ClientStateSingleton.Instance.RadioSendingState.SendingOn == 0)
                return RadioActiveTransmit;
            return Colors.Green;
        }
    }

    public bool VolumeEnabled
    {
        get
        {
            if (IsAvailable)
            {
                var currentRadio = Radio;

                if (currentRadio != null && currentRadio.Config.VolumeControl == RadioConfig.VolumeMode.OVERLAY)
                    return true;
            }

            return false;
        }
    }

    public float Volume
    {
        get
        {
            var currentRadio = Radio;

            if (currentRadio != null) return currentRadio.Volume * 100.0f;

            return 0f;
        }
        set
        {
            var currentRadio = Radio;

            if (currentRadio != null)
            {
                var clientRadio = _clientStateSingleton.PlayerUnitState.Radios[0];

                clientRadio.Volume = value / 100.0f;
            }
        }
    }

    public Command HotIntercomMicToggle { get; set; }


    public Color HotIntercomTextColour
    {
        get
        {
            if (Radio == null || !IsAvailable) return Colors.Red;

            if (ClientStateSingleton.Instance.PlayerUnitState.IntercomHotMic)
                return RadioActiveTransmit;

            return Colors.Red;
        }
    }

    public void RefreshView()
    {
        NotifyPropertyChanged(nameof(RadioActiveFill));
    }
}