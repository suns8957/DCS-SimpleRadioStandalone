using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using NAudio.Dsp;
using NAudio.Utils;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NLog;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;



namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers
{

    public class ClientEffectsPipeline
    {
        private readonly Random _random = new Random();

        private Dictionary<CachedAudioEffect.AudioEffectTypes, VolumeCachedEffectProvider> _fxProviders = new Dictionary<CachedAudioEffect.AudioEffectTypes, VolumeCachedEffectProvider>();

        private readonly CachedAudioEffectProvider effectProvider = CachedAudioEffectProvider.Instance;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private bool radioEffectsEnabled;
        private bool clippingEnabled;

        private long lastRefresh = 0; //last refresh of settings

        private readonly ProfileSettingsStore profileSettings;

        private bool radioEffects;
        private bool radioBackgroundNoiseEffect;
        private bool radioEncryptionEffect;

        private float NoiseGainOffsetDB { get; set; } = 0f;

        private bool irlRadioRXInterference = false;

        private readonly SyncedServerSettings serverSettings;

        private struct TransmissionInfo
        {
            public double Frequency;
            public Modulation Modulation;

            public short Encryption { get; internal set; }
        }

        private string PresetsFolder
        {
            get
            {
                return Path.Combine(Directory.GetCurrentDirectory(), "Presets");
            }
        }

        public ClientEffectsPipeline()
        {
            profileSettings = GlobalSettingsStore.Instance.ProfileSettingsStore;
            serverSettings = SyncedServerSettings.Instance;

            _fxProviders.Add(CachedAudioEffect.AudioEffectTypes.NATO_TONE, new VolumeCachedEffectProvider(new CachedEffectProvider(effectProvider.NATOTone)));
            _fxProviders.Add(CachedAudioEffect.AudioEffectTypes.HAVEQUICK_TONE, new VolumeCachedEffectProvider(new CachedEffectProvider(effectProvider.HAVEQUICKTone)));
            _fxProviders.Add(CachedAudioEffect.AudioEffectTypes.AM_COLLISION, new VolumeCachedEffectProvider(new CachedEffectProvider(effectProvider.AMCollision)));

            RefreshSettings();
            LoadRadioModels();
        }

        private IReadOnlyDictionary<string, RadioPreset> Presets;

        private void LoadRadioModels()
        {
            var loadedPresets = new Dictionary<string, RadioPreset>();
            try
            {
                var presets = Directory.EnumerateFiles(PresetsFolder, "*.json");
                foreach (var presetFile in presets)
                {
                    var presetName = Path.GetFileNameWithoutExtension(presetFile).ToLowerInvariant().Replace("-custom", null);
                    using (Stream jsonFile = File.OpenRead(presetFile))
                    {
                        RadioPreset preset = null;
                        try
                        {
                            preset = JsonSerializer.Deserialize<RadioPreset>(jsonFile, new JsonSerializerOptions
                            {
                                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                                AllowTrailingCommas = true,
                                ReadCommentHandling = JsonCommentHandling.Skip
                            });
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Unable to parse radio preset file {presetFile}", ex);
                        }

                        if (preset != null)
                        {
                            if (!loadedPresets.TryAdd(presetName, preset))
                            {
                                // If we load a customization afterwards, it takes precedence.
                                // If we happened to have already loaded it, ignore the 'default'.
                                if (presetName.EndsWith("-custom.json"))
                                {
                                    loadedPresets[presetName] = preset;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Unable to parse radio preset files {PresetsFolder}", ex);
            }

            Presets = loadedPresets.ToFrozenDictionary();
        }

        private void RefreshSettings()
        {
            //only get settings every 3 seconds - and cache them - issues with performance
            long now = DateTime.Now.Ticks;

            if (TimeSpan.FromTicks(now - lastRefresh).TotalSeconds > 3) //3 seconds since last refresh
            {
                lastRefresh = now;

                _fxProviders[CachedAudioEffect.AudioEffectTypes.NATO_TONE].Enabled = profileSettings.GetClientSettingBool(ProfileSettingsKeys.NATOTone);
                _fxProviders[CachedAudioEffect.AudioEffectTypes.NATO_TONE].Volume = profileSettings.GetClientSettingFloat(ProfileSettingsKeys.NATOToneVolume);

                _fxProviders[CachedAudioEffect.AudioEffectTypes.HAVEQUICK_TONE].Enabled = profileSettings.GetClientSettingBool(ProfileSettingsKeys.HAVEQUICKTone);
                _fxProviders[CachedAudioEffect.AudioEffectTypes.HAVEQUICK_TONE].Volume = profileSettings.GetClientSettingFloat(ProfileSettingsKeys.HQToneVolume);

                radioEffectsEnabled = profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioEffects);
                clippingEnabled = profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioEffectsClipping);

                radioEffects = profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioEffects);

                radioBackgroundNoiseEffect = profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioBackgroundNoiseEffect);
                radioEncryptionEffect = profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioEncryptionEffects);

                irlRadioRXInterference = serverSettings.GetSettingAsBool(ServerSettingsKeys.IRL_RADIO_RX_INTERFERENCE);

                NoiseGainOffsetDB = profileSettings.GetClientSettingFloat(ProfileSettingsKeys.NoiseGainDB);
            }
        }

        public float[] ProcessClientTransmissions(float[] tempBuffer, List<DeJitteredTransmission> transmissions, out int clientTransmissionLength)
        {
            RefreshSettings();
            DeJitteredTransmission lastTransmission = transmissions[0];

            clientTransmissionLength = 0;
            foreach (var transmission in transmissions)
            {
                for (int i = 0; i < transmission.PCMAudioLength; i++)
                {
                    tempBuffer[i] += transmission.PCMMonoAudio[i];
                }

                clientTransmissionLength = Math.Max(clientTransmissionLength, transmission.PCMAudioLength);
            }

            bool process = true;

            // take info account server setting AND volume of this radio AND if its AM or FM
            // FOR HAVEQUICK - only if its MORE THAN TWO
            if (lastTransmission.ReceivedRadio != 0
                && !lastTransmission.NoAudioEffects
                && (lastTransmission.Modulation == Modulation.AM
                    || lastTransmission.Modulation == Modulation.FM
                    || lastTransmission.Modulation == Modulation.SINCGARS
                    || lastTransmission.Modulation == Modulation.HAVEQUICK)
                && irlRadioRXInterference)
            {
                if (transmissions.Count > 1)
                {
                    //All AM is wrecked if more than one transmission
                    //For HQ - only if more than TWO transmissions and its totally fucked
                    var amCollisionProvider = _fxProviders[CachedAudioEffect.AudioEffectTypes.AM_COLLISION];
                    if (lastTransmission.Modulation == Modulation.HAVEQUICK && transmissions.Count > 2 && amCollisionProvider.Active)
                    {
                        var collisionProvider = new VolumeSampleProvider(amCollisionProvider);
                        collisionProvider.Volume = lastTransmission.Volume;

                        //replace the buffer with our own
                        collisionProvider.Read(tempBuffer, 0, clientTransmissionLength);

                        process = false;
                    }
                    else if (lastTransmission.Modulation == Modulation.AM && amCollisionProvider.Active)
                    {
                        //AM https://www.youtube.com/watch?v=yHRDjhkrDbo
                        //Heterodyne tone AND audio from multiple transmitters in a horrible mess
                        //TODO improve this


                        //process here first
                        tempBuffer = ProcessClientAudioSamples(tempBuffer, clientTransmissionLength, 0, lastTransmission);
                        process = false;

                        //apply heterodyne tone to the mixdown
                        // TODO: merge into the mixer living in ProcessClientAudioSamples().
                        var collisionMixer = new MixingSampleProvider(new List<ISampleProvider>{
                            new VolumeSampleProvider(amCollisionProvider)
                            {
                                Volume = lastTransmission.Volume,
                            },
                            new TransmissionProvider(tempBuffer, clientTransmissionLength)
                        });

                        collisionMixer.Read(tempBuffer, 0, clientTransmissionLength);
                    }
                    else if (lastTransmission.Modulation == Modulation.FM || lastTransmission.Modulation == Modulation.SINCGARS)
                    {
                        //FM picketing / picket fencing - pick one transmission at random
                        //TODO improve this to pick the stronger frequency?

                        int index = _random.Next(transmissions.Count);
                        var transmission = transmissions[index];

                        Array.Copy(transmission.PCMMonoAudio, tempBuffer, transmission.PCMMonoAudio.Length);
                        clientTransmissionLength = transmission.PCMMonoAudio.Length;
                    }
                }
            }

            //only process if AM effect doesnt apply
            if (process)
                tempBuffer = ProcessClientAudioSamples(tempBuffer, clientTransmissionLength, 0, lastTransmission);


            return tempBuffer;
        }

        public float[] ProcessClientAudioSamples(float[] buffer, int count, int offset, DeJitteredTransmission transmission)
        {
            var transmissionDetails = new TransmissionInfo
            {
                Frequency = transmission.Frequency,
                Modulation = transmission.Modulation,
                Encryption = transmission.Encryption,
            };

            ISampleProvider transmissionProvider = new TransmissionProvider(buffer, offset);
            transmissionProvider = new VolumeSampleProvider(transmissionProvider)
            {
                Volume = transmission.Volume
            };

            if (!transmission.NoAudioEffects)
            {
                if (transmission.Modulation == Modulation.MIDS
                    || transmission.Modulation == Modulation.SATCOM
                    || transmission.Modulation == Modulation.INTERCOM)
                {
                    if (radioEffects)
                    {
                        transmissionProvider = AddRadioEffectIntercom(transmissionProvider, transmissionDetails);
                    }
                }
                else
                {
                    string model = null;
                    SRClientBase sender = null;
                    if (ConnectedClientsSingleton.Instance.Clients.TryGetValue(transmission.Guid, out sender))
                    {
                        if (sender != null)
                        {
                            // Try to find which radio the transmission is coming from.
                            // "best match".
                            // #FIXME this doesn't discriminate if multiple radios are set to the same frequency
                            var candidate = Array.Find(sender.RadioInfo.radios, radio => radio.modulation == transmission.Modulation && RadioBase.FreqCloseEnough(transmission.Frequency, radio.freq));
                            
                            if (candidate != null)
                            {
                                model = candidate.Model;
                            }
                        }
                    }
                    
                    RadioPreset preset = Presets.GetValueOrDefault(model, DefaultRadioPresets.Arc210);
                    transmissionProvider = AddRadioEffect(transmissionProvider, preset, transmissionDetails);
                }
            }

            transmissionProvider.Read(buffer, offset, count);
            return buffer;
        }

        private ISampleProvider AddRadioEffectIntercom(ISampleProvider voiceProvider, TransmissionInfo details)
        {
            return BuildMicPipeline(voiceProvider, Presets.GetValueOrDefault("intercom", DefaultRadioPresets.Intercom), details);
        }

        private VolumeCachedEffectProvider GetToneProvider(Modulation modulation)
        {
            switch (modulation)
            {
                case Modulation.FM:
                case Modulation.SINCGARS:
                    return _fxProviders[CachedAudioEffect.AudioEffectTypes.NATO_TONE];

                case Modulation.HAVEQUICK:
                    return _fxProviders[CachedAudioEffect.AudioEffectTypes.HAVEQUICK_TONE];
            }

            return null;
        }

        private ISampleProvider BuildMicPipeline(ISampleProvider voiceProvider, RadioPreset radioModel, TransmissionInfo details)
        {
            voiceProvider = new FiltersProvider(voiceProvider)
            {
                Filters = radioModel.PrepassFilters
            };

            voiceProvider = new SaturationProvider(voiceProvider)
            {
                GainDB = radioModel.Saturation.Gain,
                ThresholdDB = radioModel.Saturation.Threshold
            };

            voiceProvider = new SimpleCompressorEffect(voiceProvider)
            {
                Attack = radioModel.Compressor.Attack,
                MakeUpGain = radioModel.Compressor.MakeUp,
                Release = radioModel.Compressor.Release,
                Enabled = true,
                Threshold = radioModel.Compressor.Threshold,
                Ratio = 1f / radioModel.Compressor.Slope,
            };

            // post
            voiceProvider = new FiltersProvider(voiceProvider)
            {
                Filters = radioModel.PostCompressorFilters
            };

            // Bump gain.
            voiceProvider = new VolumeSampleProvider(voiceProvider)
            {
                Volume = (float)Decibels.DecibelsToLinear(radioModel.PostGain)
            };


            var encryptionEffects = radioEncryptionEffect && (details.Modulation == Modulation.MIDS || details.Encryption > 0);
            if (encryptionEffects)
            {
                voiceProvider = new CVSDProvider(voiceProvider);
            }

            // Add receiver bandpass.
            voiceProvider = new FiltersProvider(voiceProvider)
            {
                Filters = radioModel.ReceiverFilters
            };

            return voiceProvider;
        }
        private ISampleProvider AddRadioEffect(ISampleProvider voiceProvider, RadioPreset radioModel, TransmissionInfo details)
        {

            if (radioEffectsEnabled && clippingEnabled)
            {
                voiceProvider = new ClippingProvider(voiceProvider, RadioFilter.CLIPPING_MIN, RadioFilter.CLIPPING_MAX);
            }
            
            if (radioBackgroundNoiseEffect)
            {
                // Frequency at which we switch between HF noise (very grainy/rain sounding) vs white noise.
                var hfNoiseFrequencyCutoff = 25e6;
                var backgroundEffectsProvider = new MixingSampleProvider(voiceProvider.WaveFormat);
                // Noise, initial power depends on frequency band.
                // HF very susceptible (higher base), V/UHF not as much.
                // We can do a rough estimation applying a log-based rule.
                // Rough figures for attenuation:
                // 1-30 (HF): 0-17 dB
                // 30-100: 17-23 dB
                // 100-200 (VHF): 23-26 dB
                // 200-400 (UHF): 26-29 dB
                var noiseGainDB = -Math.Log(details.Frequency/1e6) * 10 / 2;

                // #TODO: noise type should be part of the radio preset really.
                // Tube (red/pink) vs transistor (white/AGWN)
                var noiseType = details.Frequency > hfNoiseFrequencyCutoff ? SignalGeneratorType.White : SignalGeneratorType.Pink;

                // Apply user defined noise attenuation/gain
                noiseGainDB += NoiseGainOffsetDB;
                // Apply radio model noise attenuation/gain.
                noiseGainDB += radioModel.NoiseGain;

                var noiseGeneratorGainDB = details.Frequency > hfNoiseFrequencyCutoff ? noiseGainDB : 0f;

                ISampleProvider noiseProvider = new FiltersProvider(new SignalGenerator(voiceProvider.WaveFormat.SampleRate, voiceProvider.WaveFormat.Channels)
                {
                    Type = noiseType,
                    Gain = (float)Decibels.DecibelsToLinear(noiseGeneratorGainDB),
                })
                {
                    Filters = new Dsp.IFilter[] { Dsp.FirstOrderFilter.LowPass(voiceProvider.WaveFormat.SampleRate, 800) },
                };

                if (details.Frequency <= hfNoiseFrequencyCutoff)
                {
                    RadioPreset hfNoise = null;
                    if (Presets.TryGetValue("hfnoise", out hfNoise))
                    {
                        noiseProvider = BuildMicPipeline(noiseProvider, Presets["hfnoise"],
                        new TransmissionInfo
                        {
                            Frequency = details.Frequency,
                            Modulation = details.Modulation,
                            Encryption = 0
                        });
                    }

                    noiseProvider = new VolumeSampleProvider(noiseProvider)
                    {
                        Volume = (float)Decibels.DecibelsToLinear(noiseGainDB)
                    };
                }

                backgroundEffectsProvider.AddMixerInput(noiseProvider);

                var tone = GetToneProvider(details.Modulation);
                if (tone != null && tone.Active)
                {
                    backgroundEffectsProvider.AddMixerInput(tone);
                }

                // #TODO: Mix in ambient

#if false // Mains hum @ 400Hz (aviation standard)
                fxMixer.AddMixerInput(new SignalGenerator(voiceProvider.WaveFormat.SampleRate, 1)
                {
                    Type = SignalGeneratorType.SawTooth,
                    Frequency = 400,
                    Gain = (float)Decibels.DecibelsToLinear(-60),
                });
#endif
                backgroundEffectsProvider.AddMixerInput(voiceProvider);
                voiceProvider = backgroundEffectsProvider;
            }

            // NAudio version.
            // Chain of effects being applied.
            // TODO: We should be able to precompute a lot of this.
            if (radioEffectsEnabled)
            {
                voiceProvider = BuildMicPipeline(voiceProvider, radioModel, details);
            }

            voiceProvider = new ClippingProvider(voiceProvider, -1, 1);

            return voiceProvider;
        }
    }
}