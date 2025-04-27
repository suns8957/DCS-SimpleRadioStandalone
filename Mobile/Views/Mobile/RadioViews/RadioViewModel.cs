using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Mobile.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Mobile.Models.RadioChannels;
using Ciribob.DCS.SimpleRadio.Standalone.Mobile.Singleton;
using Ciribob.DCS.SimpleRadio.Standalone.Mobile.Utility;

namespace Ciribob.DCS.SimpleRadio.Standalone.Mobile.Views.Mobile.RadioViews;

public class RadioViewModel : PropertyChangedBaseClass
{
    private const double MHz = 1000000;
    private static readonly Color RadioTextGreen = Color.Parse("#00FF00");
    private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;

    public RadioViewModel(int radioId)
    {
        RadioId = radioId;
        UP0001 = new Command(() =>
        {
            RadioHelper.UpdateRadioFrequency(0.001, RadioId);
            NotifyPropertyChanged(nameof(SelectedPresetChannel));
            NotifyPropertyChanged(nameof(Frequency));
        }, () => IsAvailable);

        UP001 = new Command(() =>
        {
            RadioHelper.UpdateRadioFrequency(0.01, RadioId);
            NotifyPropertyChanged(nameof(SelectedPresetChannel));
            NotifyPropertyChanged(nameof(Frequency));
        }, () => IsAvailable);

        UP01 = new Command(() =>
        {
            RadioHelper.UpdateRadioFrequency(0.1, RadioId);
            NotifyPropertyChanged(nameof(SelectedPresetChannel));
            NotifyPropertyChanged(nameof(Frequency));
        }, () => IsAvailable);

        UP1 = new Command(() =>
        {
            RadioHelper.UpdateRadioFrequency(1, RadioId);
            NotifyPropertyChanged(nameof(SelectedPresetChannel));
            NotifyPropertyChanged(nameof(Frequency));
        }, () => IsAvailable);

        UP10 = new Command(() =>
        {
            RadioHelper.UpdateRadioFrequency(10, RadioId);
            NotifyPropertyChanged(nameof(SelectedPresetChannel));
            NotifyPropertyChanged(nameof(Frequency));
        }, () => IsAvailable);

        DOWN10 = new Command(() =>
        {
            RadioHelper.UpdateRadioFrequency(-10, RadioId);
            NotifyPropertyChanged(nameof(SelectedPresetChannel));
            NotifyPropertyChanged(nameof(Frequency));
        }, () => IsAvailable);

        DOWN1 = new Command(() =>
        {
            RadioHelper.UpdateRadioFrequency(-1, RadioId);
            NotifyPropertyChanged(nameof(SelectedPresetChannel));
            NotifyPropertyChanged(nameof(Frequency));
        }, () => IsAvailable);

        DOWN01 = new Command(() =>
        {
            RadioHelper.UpdateRadioFrequency(-0.1, RadioId);
            NotifyPropertyChanged(nameof(SelectedPresetChannel));
            NotifyPropertyChanged(nameof(Frequency));
        }, () => IsAvailable);

        DOWN001 = new Command(() =>
        {
            RadioHelper.UpdateRadioFrequency(-0.01, RadioId);
            NotifyPropertyChanged(nameof(SelectedPresetChannel));
            NotifyPropertyChanged(nameof(Frequency));
        }, () => IsAvailable);

        DOWN0001 = new Command(() =>
        {
            RadioHelper.UpdateRadioFrequency(-0.001, RadioId);
            NotifyPropertyChanged(nameof(SelectedPresetChannel));
            NotifyPropertyChanged(nameof(Frequency));
        }, () => IsAvailable);

        RadioSelect = new Command(() =>
        {
            RadioHelper.SelectRadio(RadioId);
            NotifyPropertyChanged(nameof(SelectedPresetChannel));
            NotifyPropertyChanged(nameof(Frequency));
            NotifyPropertyChanged(nameof(BackgroundActiveFill));
        }, () => IsAvailable);

        ToggleGuard = new Command(() =>
        {
            RadioHelper.ToggleGuard(RadioId);
            NotifyPropertyChanged(nameof(SelectedPresetChannel));
            NotifyPropertyChanged(nameof(Frequency));
        }, () => IsAvailable);

        ReloadCommand = new Command(() =>
        {
            Radio.ReloadChannels();
            NotifyPropertyChanged(nameof(SelectedPresetChannel));
            NotifyPropertyChanged(nameof(Radio));
            NotifyPropertyChanged(nameof(Channels));
        }, () => IsAvailable);
    }

    public string Name => Radio.Name;

    public string Frequency
    {
        get
        {
            var text = (Radio.Freq / MHz).ToString("0.000",
                CultureInfo.InvariantCulture); //make nuber UK / US style with decimals not commas!

            if (Radio.Modulation == Modulation.AM)
                text += "AM";
            else if (Radio.Modulation == Modulation.FM)
                text += "FM";
            else
                text += "";


            if (SelectedPresetChannel?.Channel > 0)
                text = $"{SelectedPresetChannel.Channel}-{SelectedPresetChannel.Text}";

            if (Radio.SecFreq > 100) text += " +G";

            return text;
        }
    }

    public Color BackgroundActiveFill
    {
        get
        {
            if (Radio == null || !IsAvailable) return Colors.Black;

            if (ClientStateSingleton.Instance.PlayerUnitState.SelectedRadio != RadioId)
                return Colors.Black;

            if (ClientStateSingleton.Instance.RadioSendingState.IsSending &&
                ClientStateSingleton.Instance.RadioSendingState.SendingOn == RadioId)
                return Colors.Magenta;
            return Colors.DarkMagenta;
        }
    }

    public Color FrequencyTextColour
    {
        get
        {
            if (Radio == null || !IsAvailable) return Colors.Red;

            try
            {
                var receivingState = ClientStateSingleton.Instance.RadioReceivingState[RadioId];

                if (receivingState.IsReceiving && receivingState.IsSecondary)
                    return Colors.Red;
                if (receivingState.IsReceiving)
                    return Colors.White;
                return RadioTextGreen;
            }
            catch
            {
            }

            return RadioTextGreen;
        }
    }

    //ADD clicks
    public ICommand UP0001 { get; set; }

    public ICommand UP01 { get; set; }

    public ICommand UP1 { get; set; }

    public ICommand UP10 { get; set; }

    public ICommand DOWN10 { get; set; }

    public ICommand DOWN1 { get; set; }

    public ICommand DOWN01 { get; set; }

    public ICommand UP001 { get; set; }

    public ICommand DOWN001 { get; set; }

    public ICommand DOWN0001 { get; set; }

    public ICommand RadioSelect { get; set; }

    public ICommand ToggleGuard { get; set; }
    public int RadioId { get; set; }

    public bool IsAvailable
    {
        get
        {
            var radio = Radio;

            if (radio == null) return false;

            return radio.Modulation != Modulation.DISABLED;
        }
    }

    public Radio Radio => ClientStateSingleton.Instance.PlayerUnitState.Radios[RadioId];

    public bool VolumeEnabled
    {
        get
        {
            if (IsAvailable)
            {
                var currentRadio = Radio;

                if (currentRadio != null && currentRadio.Config.VolumeControl)
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
                var clientRadio = _clientStateSingleton.PlayerUnitState.Radios[RadioId];

                clientRadio.Volume = value / 100.0f;
            }
        }
    }


    /**
         * PRESETS
         */
    public ICommand ReloadCommand { get; set; }


    public ObservableCollection<PresetChannel> Channels => new ObservableCollection<PresetChannel>();

    public PresetChannel SelectedPresetChannel
    {
        set
        {
            Radio.CurrentChannel = value;
            Radio.SelectRadioChannel(value);
            NotifyPropertyChanged(nameof(Frequency));
        }
        get => Radio.CurrentChannel;
    }

    public void RefreshView()
    {
        NotifyPropertyChanged(nameof(BackgroundActiveFill));
        NotifyPropertyChanged(nameof(Frequency));
        NotifyPropertyChanged(nameof(FrequencyTextColour));
        NotifyPropertyChanged(nameof(Volume));
    }
}