using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Ciribob.FS3D.SimpleRadio.Standalone.Client.Settings.RadioChannels;
using Ciribob.FS3D.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.FS3D.SimpleRadio.Standalone.Client.Singletons.Models;
using Ciribob.FS3D.SimpleRadio.Standalone.Client.Utils;
using Ciribob.SRS.Common.Helpers;
using Ciribob.SRS.Common.Network.Models;
using Ciribob.SRS.Common.Network.Singletons;

namespace Ciribob.FS3D.SimpleRadio.Standalone.Client.UI.HandheldRadioOverlayWindow;

public class HandheldRadioOverlayViewModel : PropertyChangedBase
{
    private const double MHz = 1000000;
    private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;
    private readonly ConnectedClientsSingleton _connectClientsSingleton = ConnectedClientsSingleton.Instance;
    private DispatcherTimer _updateTimer;

    public HandheldRadioOverlayViewModel(int radioId)
    {
        RadioId = radioId;
        UP0001 = new DelegateCommand(() =>
        {
            RadioHelper.UpdateRadioFrequency(0.001, RadioId);
            NotifyPropertyChanged(nameof(SelectedPresetChannel));
            NotifyPropertyChanged(nameof(Frequency));
        }, () => IsAvailable);

        UP001 = new DelegateCommand(() =>
        {
            RadioHelper.UpdateRadioFrequency(0.01, RadioId);
            NotifyPropertyChanged(nameof(SelectedPresetChannel));
            NotifyPropertyChanged(nameof(Frequency));
        }, () => IsAvailable);

        UP01 = new DelegateCommand(() =>
        {
            RadioHelper.UpdateRadioFrequency(0.1, RadioId);
            NotifyPropertyChanged(nameof(SelectedPresetChannel));
            NotifyPropertyChanged(nameof(Frequency));
        }, () => IsAvailable);

        UP1 = new DelegateCommand(() =>
        {
            RadioHelper.UpdateRadioFrequency(1, RadioId);
            NotifyPropertyChanged(nameof(SelectedPresetChannel));
            NotifyPropertyChanged(nameof(Frequency));
        }, () => IsAvailable);

        UP10 = new DelegateCommand(() =>
        {
            RadioHelper.UpdateRadioFrequency(10, RadioId);
            NotifyPropertyChanged(nameof(SelectedPresetChannel));
            NotifyPropertyChanged(nameof(Frequency));
        }, () => IsAvailable);

        DOWN10 = new DelegateCommand(() =>
        {
            RadioHelper.UpdateRadioFrequency(-10, RadioId);
            NotifyPropertyChanged(nameof(SelectedPresetChannel));
            NotifyPropertyChanged(nameof(Frequency));
        }, () => IsAvailable);

        DOWN1 = new DelegateCommand(() =>
        {
            RadioHelper.UpdateRadioFrequency(-1, RadioId);
            NotifyPropertyChanged(nameof(SelectedPresetChannel));
            NotifyPropertyChanged(nameof(Frequency));
        }, () => IsAvailable);

        DOWN01 = new DelegateCommand(() =>
        {
            RadioHelper.UpdateRadioFrequency(-0.1, RadioId);
            NotifyPropertyChanged(nameof(SelectedPresetChannel));
            NotifyPropertyChanged(nameof(Frequency));
        }, () => IsAvailable);

        DOWN001 = new DelegateCommand(() =>
        {
            RadioHelper.UpdateRadioFrequency(-0.01, RadioId);
            NotifyPropertyChanged(nameof(SelectedPresetChannel));
            NotifyPropertyChanged(nameof(Frequency));
        }, () => IsAvailable);

        DOWN0001 = new DelegateCommand(() =>
        {
            RadioHelper.UpdateRadioFrequency(-0.001, RadioId);
            NotifyPropertyChanged(nameof(SelectedPresetChannel));
            NotifyPropertyChanged(nameof(Frequency));
        }, () => IsAvailable);

        RadioSelect = new DelegateCommand(() =>
        {
            RadioHelper.SelectRadio(RadioId);
            NotifyPropertyChanged(nameof(SelectedPresetChannel));
            NotifyPropertyChanged(nameof(Frequency));
        }, () => IsAvailable);

        ToggleGuard = new DelegateCommand(() =>
        {
            RadioHelper.ToggleGuard(RadioId);
            NotifyPropertyChanged(nameof(SelectedPresetChannel));
            NotifyPropertyChanged(nameof(Frequency));
        }, () => IsAvailable);

        ReloadCommand = new DelegateCommand(() =>
        {
            Radio.ReloadChannels();
            NotifyPropertyChanged(nameof(SelectedPresetChannel));
            NotifyPropertyChanged(nameof(Radio));
            NotifyPropertyChanged(nameof(Channels));
        }, () => IsAvailable);

        DropDownClosedCommand = new DelegateCommand(() =>
        {
            //   SelectedPresetChannel = SelectedPresetChannel;
            // NotifyPropertyChanged(nameof(Radio));
            // NotifyPropertyChanged(nameof(Channels));
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
            else if (Radio.Modulation == Modulation.HAVEQUICK)
                text += "HQ";
            else
                text += "";


            if (SelectedPresetChannel?.Channel > 0)
                text = $"{SelectedPresetChannel.Channel} - {SelectedPresetChannel.Text}";

            //
            // if (currentRadio.Encrypted && currentRadio.EncryptionKey > 0)
            //     RadioFrequency.Text += " E" + currentRadio.EncryptionKey; // ENCRYPTED


            if (Radio.SecFreq > 100) text += " +G";

            return text;
        }
    }

    public SolidColorBrush RadioActiveFill
    {
        get
        {
            if (Radio == null || !IsAvailable) return new SolidColorBrush(Colors.Red);

            if (ClientStateSingleton.Instance.PlayerUnitState.SelectedRadio != RadioId)
                return new SolidColorBrush(Colors.Orange);

            if (ClientStateSingleton.Instance.RadioSendingState.IsSending &&
                ClientStateSingleton.Instance.RadioSendingState.SendingOn == RadioId)
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#96FF6D"));
            return new SolidColorBrush(Colors.Green);
        }
    }

    public SolidColorBrush FrequencyTextColour
    {
        get
        {
            if (Radio == null || !IsAvailable) return new SolidColorBrush(Colors.Red);

            try
            {
                var receivingState = ClientStateSingleton.Instance.RadioReceivingState[RadioId];

                if (receivingState.IsReceiving && receivingState.IsSecondary)
                    return new SolidColorBrush(Colors.Red);
                if (receivingState.IsReceiving)
                    return new SolidColorBrush(Colors.White);
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FF00"));
            }
            catch
            {
            }

            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FF00"));
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
                var clientRadio = _clientStateSingleton.PlayerUnitState.Radios[RadioId];

                clientRadio.Volume = value / 100.0f;
            }
        }
    }


    /**
         * PRESETS
         */
    public ICommand ReloadCommand { get; set; }

    public ICommand DropDownClosedCommand { get; set; }

    public ObservableCollection<PresetChannel> Channels => Radio.PresetChannels;

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

    public void Start()
    {
        Stop();
        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _updateTimer.Tick += RefreshView;
        _updateTimer.Start();
    }


    public void Stop()
    {
        _updateTimer?.Stop();
        _updateTimer = null;
    }

    private void RefreshView(object? sender, EventArgs e)
    {
        NotifyPropertyChanged(nameof(RadioActiveFill));
        NotifyPropertyChanged(nameof(Frequency));
        NotifyPropertyChanged(nameof(FrequencyTextColour));
        NotifyPropertyChanged(nameof(Volume));
    }

    ~HandheldRadioOverlayViewModel()
    {
        _updateTimer?.Stop();
    }
}