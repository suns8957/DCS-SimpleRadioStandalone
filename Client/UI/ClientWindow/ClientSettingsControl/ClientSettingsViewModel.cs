using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using Ciribob.FS3D.SimpleRadio.Standalone.Client.Settings;
using Ciribob.FS3D.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.FS3D.SimpleRadio.Standalone.Client.Utils;
using Ciribob.FS3D.SimpleRadio.Standalone.Common.Audio.Models;
using Ciribob.FS3D.SimpleRadio.Standalone.Common.Audio.Providers;
using Ciribob.FS3D.SimpleRadio.Standalone.Common.Settings;
using Ciribob.SRS.Common.Helpers;
using NLog;

namespace Ciribob.FS3D.SimpleRadio.Standalone.Client.UI.ClientWindow.ClientSettingsControl;

public class ClientSettingsViewModel : PropertyChangedBase
{
    private readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;
    private readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public ClientSettingsViewModel()
    {
        ResetOverlayCommand = new DelegateCommand(() =>
        {
            //TODO trigger event on messagehub
            //close overlay
            //    _radioOverlayWindow?.Close();
            //    _radioOverlayWindow = null;

            _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioX, 300);
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioY, 300);
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioWidth, 122);
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioHeight, 270);
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioOpacity, 1.0);
        });

        CreateProfileCommand = new DelegateCommand(() =>
        {
            var inputProfileWindow = new InputProfileWindow.InputProfileWindow(name =>
            {
                if (name.Trim().Length > 0)
                {
                    _globalSettings.ProfileSettingsStore.AddNewProfile(name);

                    NotifyPropertyChanged(nameof(AvailableProfiles));
                    ReloadSettings();
                }
            });
            inputProfileWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            inputProfileWindow.Owner = Application.Current.MainWindow;
            inputProfileWindow.ShowDialog();
        });

        CopyProfileCommand = new DelegateCommand(() =>
        {
            var current = _globalSettings.ProfileSettingsStore.CurrentProfileName;
            var inputProfileWindow = new InputProfileWindow.InputProfileWindow(name =>
            {
                if (name.Trim().Length > 0)
                {
                    _globalSettings.ProfileSettingsStore.CopyProfile(current, name);
                    NotifyPropertyChanged(nameof(AvailableProfiles));
                    ReloadSettings();
                }
            });
            inputProfileWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            inputProfileWindow.Owner = Application.Current.MainWindow;
            inputProfileWindow.ShowDialog();
        });

        RenameProfileCommand = new DelegateCommand(() =>
        {
            var current = _globalSettings.ProfileSettingsStore.CurrentProfileName;
            if (current.Equals("default"))
            {
                MessageBox.Show(Application.Current.MainWindow,
                    "Cannot rename the default input!",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            else
            {
                var oldName = current;
                var inputProfileWindow = new InputProfileWindow.InputProfileWindow(name =>
                {
                    if (name.Trim().Length > 0)
                    {
                        _globalSettings.ProfileSettingsStore.RenameProfile(oldName, name);
                        SelectedProfile = _globalSettings.ProfileSettingsStore.CurrentProfileName;
                        NotifyPropertyChanged(nameof(AvailableProfiles));
                        ReloadSettings();
                    }
                }, true, oldName);
                inputProfileWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                inputProfileWindow.Owner = Application.Current.MainWindow;
                inputProfileWindow.ShowDialog();
            }
        });

        DeleteProfileCommand = new DelegateCommand(() =>
        {
            var current = _globalSettings.ProfileSettingsStore.CurrentProfileName;

            if (current.Equals("default"))
            {
                MessageBox.Show(Application.Current.MainWindow,
                    "Cannot delete the default input!",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            else
            {
                var result = MessageBox.Show(Application.Current.MainWindow,
                    $"Are you sure you want to delete {current} ?",
                    "Confirmation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    _globalSettings.ProfileSettingsStore.RemoveProfile(current);
                    SelectedProfile = _globalSettings.ProfileSettingsStore.CurrentProfileName;
                    NotifyPropertyChanged(nameof(AvailableProfiles));
                    ReloadSettings();
                }
            }
        });
    }

    public ICommand ResetOverlayCommand { get; set; }

    public ICommand CreateProfileCommand { get; set; }
    public ICommand CopyProfileCommand { get; set; }
    public ICommand RenameProfileCommand { get; set; }
    public ICommand DeleteProfileCommand { get; set; }

    /**
         * Global Settings
         */

    public bool AllowMoreInputs
    {
        get => _globalSettings.GetClientSettingBool(GlobalSettingsKeys.ExpandControls);
        set
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.ExpandControls, value);
            NotifyPropertyChanged();
            MessageBox.Show(
                "You must restart SRS for this setting to take effect.\n\nTurning this on will allow almost any DirectX device to be used as input expect a Mouse but may cause issues with other devices being detected. \n\nUse device white listing instead",
                "Restart SRS", MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    public bool MinimiseToTray
    {
        get => _globalSettings.GetClientSettingBool(GlobalSettingsKeys.MinimiseToTray);
        set
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.MinimiseToTray, value);
            NotifyPropertyChanged();
        }
    }

    public bool StartMinimised
    {
        get => _globalSettings.GetClientSettingBool(GlobalSettingsKeys.StartMinimised);
        set
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.StartMinimised, value);
            NotifyPropertyChanged();
        }
    }

    public bool MicAGC
    {
        get => _globalSettings.GetClientSettingBool(GlobalSettingsKeys.AGC);
        set
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.AGC, value);
            NotifyPropertyChanged();
        }
    }

    public bool MicDenoise
    {
        get => _globalSettings.GetClientSettingBool(GlobalSettingsKeys.Denoise);
        set
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.Denoise, value);
            NotifyPropertyChanged();
        }
    }

    public bool PlayConnectionSounds
    {
        get => _globalSettings.GetClientSettingBool(GlobalSettingsKeys.PlayConnectionSounds);
        set
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.PlayConnectionSounds, value);
            NotifyPropertyChanged();
        }
    }

    /**
         * Profile Settings
         */

    public bool RadioSwitchIsPTT
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioSwitchIsPTT);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.RadioSwitchIsPTT, value);
            NotifyPropertyChanged();
        }
    }

    public bool AutoSelectChannel
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(
            ProfileSettingsKeys.AutoSelectPresetChannel);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.AutoSelectPresetChannel,
                value);
            NotifyPropertyChanged();
        }
    }

    public float PTTReleaseDelay
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.PTTReleaseDelay);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.PTTReleaseDelay, value);
            NotifyPropertyChanged();
        }
    }

    public float PTTStartDelay
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.PTTStartDelay);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.PTTStartDelay, value);
            NotifyPropertyChanged();
        }
    }

    public bool RadioRxStartToggle
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioRxEffects_Start);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.RadioRxEffects_Start,
                value);
            NotifyPropertyChanged();
        }
    }

    public bool RadioRxEndToggle
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioRxEffects_End);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.RadioRxEffects_End,
                value);
            NotifyPropertyChanged();
        }
    }

    public bool RadioTxStartToggle
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioTxEffects_Start);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.RadioTxEffects_Start,
                value);
            NotifyPropertyChanged();
        }
    }

    public bool RadioTxEndToggle
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioRxEffects_End);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.RadioRxEffects_End,
                value);
            NotifyPropertyChanged();
        }
    }

    public List<CachedAudioEffect> RadioTransmissionStart =>
        CachedAudioEffectProvider.Instance.RadioTransmissionStart;

    public CachedAudioEffect SelectedRadioTransmissionStartEffect
    {
        set
        {
            GlobalSettingsStore.Instance.ProfileSettingsStore.SetClientSettingString(
                ProfileSettingsKeys.RadioTransmissionStartSelection, value.FileName);
            NotifyPropertyChanged();
        }
        get => CachedAudioEffectProvider.Instance.SelectedRadioTransmissionStartEffect;
    }

    public List<CachedAudioEffect> RadioTransmissionEnd => CachedAudioEffectProvider.Instance.RadioTransmissionEnd;

    public CachedAudioEffect SelectedRadioTransmissionEndEffect
    {
        set
        {
            GlobalSettingsStore.Instance.ProfileSettingsStore.SetClientSettingString(
                ProfileSettingsKeys.RadioTransmissionEndSelection, value.FileName);
            NotifyPropertyChanged();
        }
        get => CachedAudioEffectProvider.Instance.SelectedRadioTransmissionEndEffect;
    }

    public bool RadioSoundEffectsToggle
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioEffects);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.RadioEffects, value);
            NotifyPropertyChanged();
        }
    }

    public bool RadioEffectsClippingToggle
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioEffectsClipping);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.RadioEffectsClipping,
                value);
            NotifyPropertyChanged();
        }
    }

    public bool FMRadioToneToggle
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.NATOTone);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.NATOTone, value);
            NotifyPropertyChanged();
        }
    }

    public double FMRadioToneVolume
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.NATOToneVolume)
            / double.Parse(
                ProfileSettingsStore.DefaultSettingsProfileSettings[ProfileSettingsKeys.NATOToneVolume.ToString()],
                CultureInfo.InvariantCulture) * 100;
        set
        {
            var orig = double.Parse(
                ProfileSettingsStore.DefaultSettingsProfileSettings[ProfileSettingsKeys.NATOToneVolume.ToString()],
                CultureInfo.InvariantCulture);
            var vol = orig * (value / 100);

            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.NATOToneVolume,
                (float)vol);
            NotifyPropertyChanged();
        }
    }

    public bool BackgroundRadioNoiseToggle
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys
            .RadioBackgroundNoiseEffect);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(
                ProfileSettingsKeys.RadioBackgroundNoiseEffect, value);
            NotifyPropertyChanged();
        }
    }

    public double UHFEffectVolume
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.UHFNoiseVolume)
            / double.Parse(
                ProfileSettingsStore.DefaultSettingsProfileSettings[ProfileSettingsKeys.UHFNoiseVolume.ToString()],
                CultureInfo.InvariantCulture) * 100;
        set
        {
            var orig = double.Parse(
                ProfileSettingsStore.DefaultSettingsProfileSettings[ProfileSettingsKeys.UHFNoiseVolume.ToString()],
                CultureInfo.InvariantCulture);
            var vol = orig * (value / 100);

            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.UHFNoiseVolume,
                (float)vol);
            NotifyPropertyChanged();
        }
    }

    public double VHFEffectVolume
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.VHFNoiseVolume)
            / double.Parse(
                ProfileSettingsStore.DefaultSettingsProfileSettings[ProfileSettingsKeys.VHFNoiseVolume.ToString()],
                CultureInfo.InvariantCulture) * 100;
        set
        {
            var orig = double.Parse(
                ProfileSettingsStore.DefaultSettingsProfileSettings[ProfileSettingsKeys.VHFNoiseVolume.ToString()],
                CultureInfo.InvariantCulture);
            var vol = orig * (value / 100);

            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.VHFNoiseVolume,
                (float)vol);
            NotifyPropertyChanged();
        }
    }

    public double HFEffectVolume
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.HFNoiseVolume)
            / double.Parse(
                ProfileSettingsStore.DefaultSettingsProfileSettings[ProfileSettingsKeys.HFNoiseVolume.ToString()],
                CultureInfo.InvariantCulture) * 100;
        set
        {
            var orig = double.Parse(
                ProfileSettingsStore.DefaultSettingsProfileSettings[ProfileSettingsKeys.HFNoiseVolume.ToString()],
                CultureInfo.InvariantCulture);
            var vol = orig * (value / 100);

            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.HFNoiseVolume,
                (float)vol);
            NotifyPropertyChanged();
        }
    }

    public double FMEffectVolume
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.FMNoiseVolume)
            / double.Parse(
                ProfileSettingsStore.DefaultSettingsProfileSettings[ProfileSettingsKeys.FMNoiseVolume.ToString()],
                CultureInfo.InvariantCulture) * 100;
        set
        {
            var orig = double.Parse(
                ProfileSettingsStore.DefaultSettingsProfileSettings[ProfileSettingsKeys.FMNoiseVolume.ToString()],
                CultureInfo.InvariantCulture);
            var vol = orig * (value / 100);

            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.FMNoiseVolume,
                (float)vol);
            NotifyPropertyChanged();
        }
    }

    public double GroundEffectVolume
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.GroundNoiseVolume)
            / double.Parse(
                ProfileSettingsStore.DefaultSettingsProfileSettings[ProfileSettingsKeys.GroundNoiseVolume.ToString()],
                CultureInfo.InvariantCulture) * 100;
        set
        {
            var orig = double.Parse(
                ProfileSettingsStore.DefaultSettingsProfileSettings[ProfileSettingsKeys.GroundNoiseVolume.ToString()],
                CultureInfo.InvariantCulture);
            var vol = orig * (value / 100);

            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.GroundNoiseVolume,
                (float)vol);
            NotifyPropertyChanged();
        }
    }

    public double AircraftEffectVolume
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.AircraftNoiseVolume)
            / double.Parse(
                ProfileSettingsStore.DefaultSettingsProfileSettings[ProfileSettingsKeys.AircraftNoiseVolume.ToString()],
                CultureInfo.InvariantCulture) * 100;
        set
        {
            var orig = double.Parse(
                ProfileSettingsStore.DefaultSettingsProfileSettings[ProfileSettingsKeys.AircraftNoiseVolume.ToString()],
                CultureInfo.InvariantCulture);
            var vol = orig * (value / 100);

            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.AircraftNoiseVolume,
                (float)vol);
            NotifyPropertyChanged();
        }
    }

    /**
         * Radio Audio Balance
         */

    public float RadioChannel1
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.Radio1Channel);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.Radio1Channel, value);
            NotifyPropertyChanged();
        }
    }

    public float RadioChannel2
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.Radio2Channel);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.Radio2Channel, value);
            NotifyPropertyChanged();
        }
    }

    public float RadioChannel3
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.Radio3Channel);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.Radio3Channel, value);
            NotifyPropertyChanged();
        }
    }

    public float RadioChannel4
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.Radio4Channel);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.Radio4Channel, value);
            NotifyPropertyChanged();
        }
    }

    public float RadioChannel5
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.Radio5Channel);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.Radio5Channel, value);
            NotifyPropertyChanged();
        }
    }

    public float RadioChannel6
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.Radio6Channel);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.Radio6Channel, value);
            NotifyPropertyChanged();
        }
    }

    public float RadioChannel7
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.Radio7Channel);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.Radio7Channel, value);
            NotifyPropertyChanged();
        }
    }

    public float RadioChannel8
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.Radio8Channel);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.Radio8Channel, value);
            NotifyPropertyChanged();
        }
    }

    public float RadioChannel9
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.Radio9Channel);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.Radio9Channel, value);
            NotifyPropertyChanged();
        }
    }

    public float RadioChannel10
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.Radio10Channel);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.Radio10Channel, value);
            NotifyPropertyChanged();
        }
    }

    public float Intercom
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.IntercomChannel);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.IntercomChannel, value);
            NotifyPropertyChanged();
        }
    }

    public string SelectedProfile
    {
        set
        {
            if (value != null)
            {
                _globalSettings.ProfileSettingsStore.CurrentProfileName = value;
                //TODO send event notifying of change to current profile
                ReloadSettings();
                // EventBus.Instance.PublishOnUIThreadAsync(new ProfileChangedMessage());
            }

            NotifyPropertyChanged();
        }
        get => _globalSettings.ProfileSettingsStore.CurrentProfileName;
    }

    public List<string> AvailableProfiles
    {
        set
        {
            //do nothing
        }
        get => _globalSettings.ProfileSettingsStore.ProfileNames;
    }

    public string PlayerName
    {
        set
        {
            if (value != null) ClientStateSingleton.Instance.PlayerUnitState.Name = value;
        }
        get => ClientStateSingleton.Instance.PlayerUnitState.Name;
    }

    public uint PlayerID
    {
        set
        {
            if (value != null) ClientStateSingleton.Instance.PlayerUnitState.UnitId = value;
        }
        get => ClientStateSingleton.Instance.PlayerUnitState.UnitId;
    }

    private void ReloadSettings()
    {
        NotifyPropertyChanged(nameof(MinimiseToTray));
        NotifyPropertyChanged(nameof(StartMinimised));
        NotifyPropertyChanged(nameof(MicAGC));
        NotifyPropertyChanged(nameof(MicDenoise));
        NotifyPropertyChanged(nameof(PlayConnectionSounds));
        NotifyPropertyChanged(nameof(PlayerName));
        NotifyPropertyChanged(nameof(PlayerID));
        NotifyPropertyChanged(nameof(RadioSwitchIsPTT));
        NotifyPropertyChanged(nameof(AutoSelectChannel));
        NotifyPropertyChanged(nameof(PTTReleaseDelay));
        NotifyPropertyChanged(nameof(PTTStartDelay));
        NotifyPropertyChanged(nameof(RadioRxStartToggle));
        NotifyPropertyChanged(nameof(RadioRxEndToggle));
        NotifyPropertyChanged(nameof(RadioTxStartToggle));
        NotifyPropertyChanged(nameof(RadioTxEndToggle));
        NotifyPropertyChanged(nameof(SelectedRadioTransmissionStartEffect));
        NotifyPropertyChanged(nameof(SelectedRadioTransmissionEndEffect));
        NotifyPropertyChanged(nameof(RadioSoundEffectsToggle));
        NotifyPropertyChanged(nameof(RadioEffectsClippingToggle));
        NotifyPropertyChanged(nameof(FMRadioToneToggle));
        NotifyPropertyChanged(nameof(FMRadioToneVolume));
        NotifyPropertyChanged(nameof(BackgroundRadioNoiseToggle));
        NotifyPropertyChanged(nameof(UHFEffectVolume));
        NotifyPropertyChanged(nameof(VHFEffectVolume));
        NotifyPropertyChanged(nameof(HFEffectVolume));
        NotifyPropertyChanged(nameof(FMEffectVolume));
        NotifyPropertyChanged(nameof(RadioChannel1));
        NotifyPropertyChanged(nameof(RadioChannel2));
        NotifyPropertyChanged(nameof(RadioChannel3));
        NotifyPropertyChanged(nameof(RadioChannel4));
        NotifyPropertyChanged(nameof(RadioChannel5));
        NotifyPropertyChanged(nameof(RadioChannel6));
        NotifyPropertyChanged(nameof(RadioChannel7));
        NotifyPropertyChanged(nameof(RadioChannel8));
        NotifyPropertyChanged(nameof(RadioChannel9));
        NotifyPropertyChanged(nameof(RadioChannel10));
        NotifyPropertyChanged(nameof(Intercom));
        NotifyPropertyChanged(nameof(SelectedProfile));

        //TODO send message to tell input to reload!
    }
}