using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Ciribob.FS3D.SimpleRadio.Standalone.Client.Settings.RadioChannels;
using Ciribob.SRS.Common.Helpers;
using Ciribob.SRS.Common.Network.Client;
using Ciribob.SRS.Common.Network.Models;
using Ciribob.SRS.Common.Network.Singletons;
using Newtonsoft.Json;
using NLog;

namespace Ciribob.FS3D.SimpleRadio.Standalone.Client.Singletons.Models;

public class Radio : PropertyChangedBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private string _name = "";

    public Radio()
    {
        ReloadChannels();
    }

    public RadioConfig Config { get; set; } = new();

    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            ReloadChannels();
        }
    }

    public double Freq { get; set; } = 1;
    public Modulation Modulation { get; set; } = Modulation.DISABLED;

    public bool Encrypted { get; set; } = false;
    public byte EncKey { get; set; } = 0;

    public double SecFreq { get; set; } = 1;

    //should the radio restransmit?
    public bool Retransmit { get; set; }
    public float Volume { get; set; } = 1.0f;
    public PresetChannel CurrentChannel { get; set; }
    public bool SimultaneousTransmission { get; set; }

    //Channels
    public ObservableCollection<PresetChannel> PresetChannels { get; } = new();

    public RadioBase RadioBase =>
        new()
        {
            Encrypted = Encrypted,
            EncKey = EncKey,
            Modulation = Modulation,
            Freq = Freq,
            SecFreq = SecFreq
        };

    /**
     * Used to determine if we should send an update to the server or not
     * We only need to do that if something that would stop us Receiving happens which
     * is frequencies and modulation
     */
    public bool Available()
    {
        return Modulation != Modulation.DISABLED;
    }

    public void ReloadChannels()
    {
        PresetChannels.Clear();
        PresetChannels.Add(new PresetChannel { Channel = 0, Text = "No Channel", Value = 0 });
        if (Name.Length == 0 || !Available()) return;
        foreach (var presetChannel in new FilePresetChannelsStore().LoadFromStore(Name))
        {
            var freq = (double)presetChannel.Value;
            if (freq < Config.MaxFrequency && freq > Config.MinimumFrequency)
            {
                PresetChannels.Add(presetChannel);
                Logger.Info($"Added {presetChannel.Text} for radio {Name} with frequency {freq}");
            }
            else
            {
                Logger.Error(
                    $"Unable to add {presetChannel.Text} for radio {Name} with frequency {freq} - outside of radio range");
            }
        }

        CurrentChannel = PresetChannels.First();
    }

    public override bool Equals(object obj)
    {
        if (obj == null || GetType() != obj.GetType())
            return false;

        var compare = (Radio)obj;

        if (!Name.Equals(compare.Name)) return false;
        if (!RadioBase.FreqCloseEnough(Freq, compare.Freq)) return false;
        if (Modulation != compare.Modulation) return false;
        if (Encrypted != compare.Encrypted) return false;
        if (EncKey != compare.EncKey) return false;
        if (Retransmit != compare.Retransmit) return false;
        if (!RadioBase.FreqCloseEnough(SecFreq, compare.SecFreq)) return false;

        if (Config != null && compare.Config == null) return false;
        if (Config == null && compare.Config != null) return false;

        return Config.Equals(compare.Config);
    }


    public static List<Radio> LoadRadioConfig(string file)
    {
        var loadedConfig = new Radio[11];

        for (var i = 0; i < 11; i++)
            loadedConfig[i] = new Radio
            {
                Config = new RadioConfig
                {
                    MinimumFrequency = 1,
                    MaxFrequency = 1,
                    FrequencyControl = RadioConfig.FreqMode.COCKPIT,
                    VolumeControl = RadioConfig.VolumeMode.COCKPIT,
                    GuardFrequency = 0
                },
                Freq = 1,
                SecFreq = 0,
                Modulation = Modulation.DISABLED,
                Name = "Invalid Config"
            };

        try
        {
            var radioJson = File.ReadAllText(file);
            var loadedList = JsonConvert.DeserializeObject<Radio[]>(radioJson);

            //      if (loadedList.Length < 2) throw new Exception("Not enough radios configured");

            for (var i = 0; i < loadedList.Length; i++)
                //copy the valid ones
                loadedConfig[i] = loadedList[i];

            foreach (var radio in loadedConfig)
                if (radio.PresetChannels.Count >= 2)
                    //select first channel (0 is no channel)
                    radio.CurrentChannel = radio.PresetChannels[1];
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to load " + file);

            loadedConfig = new Radio[11];
            for (var i = 0; i < 11; i++)
                loadedConfig[i] = new Radio
                {
                    Config = new RadioConfig
                    {
                        MinimumFrequency = 1,
                        MaxFrequency = 1,
                        FrequencyControl = RadioConfig.FreqMode.COCKPIT,
                        VolumeControl = RadioConfig.VolumeMode.COCKPIT,
                        GuardFrequency = 0
                    },
                    Freq = 1,
                    SecFreq = 0,
                    Modulation = Modulation.DISABLED,
                    Name = "Invalid Config"
                };

            loadedConfig[1] = new Radio
            {
                Freq = 1.51e+8,
                Config = new RadioConfig
                {
                    MinimumFrequency = 1.0e+8,
                    MaxFrequency = 3.51e+8,
                    FrequencyControl = RadioConfig.FreqMode.OVERLAY,
                    VolumeControl = RadioConfig.VolumeMode.OVERLAY,
                    GuardFrequency = 1.215e+8
                },
                SecFreq = 0,
                Modulation = Modulation.AM,
                Name = "BK RADIO"
            };
        }

        return new List<Radio>(loadedConfig);
    }

    public void SelectRadioChannel(PresetChannel value)
    {
        if (value == null)
        {
            if (PresetChannels.Count > 0)
                CurrentChannel = PresetChannels.First();
            else
                value = null;
        }
        else
        {
            CurrentChannel = value;

            if (value.Channel > 0)
            {
                Freq = (double)CurrentChannel.Value;
                EventBus.Instance.PublishOnBackgroundThreadAsync(new UnitUpdateMessage
                {
                    FullUpdate = true, UnitUpdate = ClientStateSingleton.Instance.PlayerUnitState.PlayerUnitStateBase
                });
            }
        }
    }
}