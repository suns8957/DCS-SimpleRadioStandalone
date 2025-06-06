using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers.Wave;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using MathNet.Numerics.Distributions;
using NAudio.Codecs;
using NAudio.Dsp;
using NAudio.Utils;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;



namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers
{
    namespace Dsp
    {
        interface IFilter
        {
            float Transform(float input);
        }

        // https://dsp.stackexchange.com/a/93451
        class FirstOrderFilter : IFilter
        {
            private double b0;
            private double b1;
            private double a1;

            private float x_n;
            private float x_n1;
            private float y_n1;

            private void SetCoefficients(double aa0, double bb0, double bb1, double aa1)
            {
                b0 = bb0 / aa0;
                b1 = bb1 / aa0;
                a1 = aa1 / aa0;
            }

            public static FirstOrderFilter LowPass(float sampleRate, float cutoffFrequency)
            {
                // H(s) = 1 / (1 + s)
                var filter = new FirstOrderFilter();
                var w0 = 2 * Math.PI * cutoffFrequency / sampleRate;
                var (sinw0, cosw0) = Math.SinCos(w0);

                var a0 = sinw0 + 1 + cosw0;
                var a1 = sinw0 - 1 - cosw0;
                var b0 = sinw0;
                var b1 = sinw0;

                filter.SetCoefficients(a0, b0, b1, a1);
                return filter;
            }

            public static FirstOrderFilter HighPass(float sampleRate, float cutoffFrequency)
            {
                // H(s) = s / (1 + s)
                var filter = new FirstOrderFilter();
                var w0 = 2 * Math.PI * cutoffFrequency / sampleRate;
                var (sinw0, cosw0) = Math.SinCos(w0);

                var a0 = sinw0 + 1 + cosw0;
                var a1 = sinw0 - 1 - cosw0;
                var b0 = 1 + cosw0;
                var b1 = -1 - cosw0;

                filter.SetCoefficients(a0, b0, b1, a1);
                return filter;
            }

            public float Transform(float input)
            {
                y_n1 = (float)(b0 * input + b1 * x_n1 - a1 * y_n1);
                x_n1 = input;

                return y_n1;
            }
        }

        class BiQuadFilter : IFilter
        {
            public NAudio.Dsp.BiQuadFilter Filter { get; set; }
            public float Transform(float input)
            {
                return Filter.Transform(input);
            }
        }
    }

    namespace Wave
    {
        class GaussianWhiteNoise : ISampleProvider
        {
            public float Gain { get; set; } = 1.0f;
            private Normal Noise = new Normal(0, Math.Sqrt(2) / 2);

            public WaveFormat WaveFormat { get; private set; }

            public GaussianWhiteNoise(int sampleRate, int channels)
            {
                WaveFormat = new WaveFormat(sampleRate, channels);
            }

            public int Read(float[] buffer, int offset, int count)
            {
                var power = Gain;
                for (int sampleCount = 0; sampleCount < count / WaveFormat.Channels; sampleCount++)
                {
                    buffer[offset + sampleCount] = (float)(power * Noise.Sample());
                }

                return count;
            }
        }
        class BiQuadProvider : ISampleProvider
        {
            public Dsp.IFilter[] Filters { get; set; }
            ISampleProvider Source;

            public BiQuadProvider(ISampleProvider source)
            {
                Source = source;
            }

            public WaveFormat WaveFormat => Source.WaveFormat;

            public int Read(float[] buffer, int offset, int count)
            {
                var samplesRead = Source.Read(buffer, offset, count);
                for (int i = 0; i < count; ++i)
                {
                    var source = buffer[offset + i];
                    foreach (var filter in Filters)
                    {
                            source = filter.Transform(source);
                    }
                    buffer[offset + i] = source;
                }

                return samplesRead;
            }
        }
        class TransmissionProvider : ISampleProvider
        {
            public WaveFormat WaveFormat => WaveFormat.CreateIeeeFloatWaveFormat(Constants.OUTPUT_SAMPLE_RATE, 1);
            private float[] Buffer;
            private int Offset;
            public TransmissionProvider(float[] buffer, int offset)
            {
                Buffer = buffer;
                Offset = offset;
            }

            public int Read(float[] buffer, int offset, int count)
            {
                count = Math.Min(Buffer.Length - Offset, count);
                Array.Copy(Buffer, Offset, buffer, offset, count);

                return count;
            }
        }
        class CachedEffectProvider : ISampleProvider
        {
            public WaveFormat WaveFormat => WaveFormat.CreateIeeeFloatWaveFormat(Constants.OUTPUT_SAMPLE_RATE, 1);

            public int Read(float[] buffer, int offset, int count)
            {
                var processed = 0;
                do
                {
                    var availableSamples = Effect.AudioEffectFloat.Length - Position;
                    var samplesToCopy = Math.Min(availableSamples, count - processed);
                    Array.Copy(Effect.AudioEffectFloat, Position, buffer, offset + processed, samplesToCopy);
                    Position += samplesToCopy;

                    if (Position == Effect.AudioEffectFloat.Length)
                    {
                        Position = PositionRollover(Position, Effect.AudioEffectFloat.Length);
                    }

                    processed += samplesToCopy;
                } while (processed < count);

                return processed;
            }

            public bool Enabled { get; set; } = true;
            public bool Active => Enabled && Effect.Loaded;


            private int Position { get; set; } = 0;


            private CachedAudioEffect Effect;

            public CachedEffectProvider(CachedAudioEffect Effect)
            {
                this.Effect = Effect;
            }

            protected virtual int PositionRollover(int position, int toneLength)
            {
                if (position == toneLength)
                {
                    position = 0;
                }

                return position;
            }
        }

        class VolumeCachedEffectProvider : ISampleProvider
        {
            private readonly VolumeSampleProvider volumeProvider;
            private readonly CachedEffectProvider effectProvider;

            public WaveFormat WaveFormat => volumeProvider.WaveFormat;

            public int Read(float[] buffer, int offset, int count)
            {
                return volumeProvider.Read(buffer, offset, count);
            }

            public VolumeCachedEffectProvider(CachedEffectProvider effectProvider)
            {
                this.effectProvider = effectProvider;
                volumeProvider = new VolumeSampleProvider(effectProvider);
            }

            public float Volume
            {
                get { return volumeProvider.Volume; }
                set
                {
                    volumeProvider.Volume = value;
                }
            }

            public bool Enabled
            {
                get { return effectProvider.Enabled; }
                set { effectProvider.Enabled = value; }
            }

            public bool Active => effectProvider.Active;
        }

        class ClippingProvider : ISampleProvider
        {
            private float Min { get; set; }
            private float Max { get; set; }

            public WaveFormat WaveFormat => Source.WaveFormat;

            private ISampleProvider Source;

            public ClippingProvider(ISampleProvider source, float min, float max)
            {
                Source = source;
                Min = min;
                Max = max;
            }

            public int Read(float[] buffer, int offset, int count)
            {
                int samplesRead = Source.Read(buffer, offset, count);
                for (int i = 0; i < count; ++i)
                {
                    buffer[offset + i] = Math.Max(Math.Min(buffer[offset + i], Max), Min);
                }
                return samplesRead;
            }
        }

        class ALawProvider : ISampleProvider
        {
            private ISampleProvider source;
            public WaveFormat WaveFormat => source.WaveFormat;

            public ALawProvider(ISampleProvider source)
            {
                this.source = source;
            }

            public int Read(float[] buffer, int offset, int count)
            {
                var samplesRead = source.Read(buffer, offset, count);
                for (int i = 0; i < count; ++i)
                {
                    // A-law works with 13-bit signed integer samples - 2^13 = 8192.
                    // This'll introduce some additional 'loss precsion' contributing to the effect..
                    buffer[offset + i] = ALawDecoder.ALawToLinearSample(ALawEncoder.LinearToALawSample((short)(buffer[offset + i] * 8192))) / 8192f;
                }

                return samplesRead;
            }
        }

        class SaturationProvider : ISampleProvider
        {
            private ISampleProvider source;

            private float _gainLinear;
            public float GainDB
            {
                get
                {
                    return (float)Decibels.LinearToDecibels(_gainLinear);
                }
                set
                {
                    _gainLinear = (float)Decibels.DecibelsToLinear(value);
                }
            }

            private float _thresholdLinear;
            public float ThresholdDB
            {
                get
                {
                    return (float)Decibels.LinearToDecibels(_thresholdLinear);
                }
                set
                {
                  _thresholdLinear = (float)Decibels.DecibelsToLinear(value);  
                }
            }

            private float _dryGainLinear = (float)Decibels.DecibelsToLinear(0f);
            private float _wetGainLinear = (float)Decibels.DecibelsToLinear(1f);

            public WaveFormat WaveFormat => source.WaveFormat;
            public SaturationProvider(ISampleProvider source)
            {
                this.source = source;
            }

            public int Read(float[] buffer, int offset, int count)
            {
                var samplesRead = source.Read(buffer, offset, count);
                for (int i = 0; i < count; ++i)
                {
                    var dry = buffer[offset + i];
                    var wet = 0f;
                    var absDry = Math.Abs(dry);
                    if (absDry > _thresholdLinear)
                    {
                        // dry clips at threshold.
                        dry = (float)Math.CopySign(_thresholdLinear, dry);
                        
                        
                        // overdrive part.
                        wet = (float)Math.CopySign(absDry - _thresholdLinear, dry) * _wetGainLinear;
                    }

                    buffer[offset + i] = _gainLinear * dry + wet / _gainLinear;
                }

                return samplesRead;
            }
        }

        class CVSDProvider : ISampleProvider
        {
            private ISampleProvider source;
            private class CVSD
            {
                public int coincidences = 0; // 3-bit coincidence @ 16k Hz. We're sampling 3x that, so 9-bit.
                public float step = 0; // Current step size
                public float product = 0; // comparator for quantization
                public readonly int coincidenceBits;

                public CVSD()
                {
                    coincidenceBits = 0b111;// (1 << (new Random().Next(3, 10))) - 1;// 0b111;
                }


                // Encoder settings.
                public float syllabic = 0;
                public static readonly float BETA_SYLLABIC = 0.9f;
                public static readonly float DELTA_MAX = 0.1f;// / Math.Max(Constants.OUTPUT_SAMPLE_RATE / (float)Constants.MIC_SAMPLE_RATE, 1);
                public static readonly float DELTA_MIN = DELTA_MAX / 20;
                public static readonly float DELTA_NAUGHT = DELTA_MAX * (1.0f - BETA_SYLLABIC);
                public static readonly float BETA_RECONSTRUCTION = 0.9394f;
                public static readonly float ALPHA_RECONSTRUCTION = 1;
            }

            private static CVSD cvsd = new CVSD();

            public WaveFormat WaveFormat => source.WaveFormat;

            public CVSDProvider(ISampleProvider source)
            {
                this.source = source;
            }

            public int Read(float[] buffer, int offset, int count)
            {
                var samplesRead = source.Read(buffer, offset, count);
                for (int i = 0; i < count; ++i)
                {
                    int coincidenceBits = cvsd.coincidenceBits;
                    // 3-bit accumulator. (9-bit becaues we're at 48kHz and the algorithm is for 16 kHz)
                    cvsd.coincidences = ((cvsd.coincidences << 1) & coincidenceBits);
                    // Compute coincidence.
                    var coincidence = buffer[offset + i] > cvsd.product;
                    // Insert one if the current signal is greater than we have already quantized.
                    cvsd.coincidences |= coincidence ? 1 : 0;

                    var runOfCoincidences = cvsd.coincidences == coincidenceBits || cvsd.coincidences == 0;
                    cvsd.syllabic = CVSD.DELTA_NAUGHT * (runOfCoincidences ? 1 : 0) + CVSD.BETA_SYLLABIC * cvsd.syllabic;


                    var symbol = (coincidence ? 1 : -1) * (cvsd.syllabic + CVSD.DELTA_MIN);
                    cvsd.product = CVSD.ALPHA_RECONSTRUCTION * symbol + CVSD.BETA_RECONSTRUCTION * cvsd.product;

                    buffer[offset + i] = cvsd.product;
                }

                return samplesRead;
            }
        }
    }


    public class ClientEffectsPipeline
    {
        private readonly Random _random = new Random();

        private Dictionary<CachedAudioEffect.AudioEffectTypes, VolumeCachedEffectProvider> _fxProviders = new Dictionary<CachedAudioEffect.AudioEffectTypes, VolumeCachedEffectProvider>();

        private readonly BiQuadFilter _highPassFilter;
        private readonly BiQuadFilter _lowPassFilter;

        private readonly CachedAudioEffectProvider effectProvider = CachedAudioEffectProvider.Instance;

        private bool radioEffectsEnabled;
        private bool clippingEnabled;

        private long lastRefresh = 0; //last refresh of settings

        private readonly ProfileSettingsStore profileSettings;

        private bool radioEffects;
        private bool radioBackgroundNoiseEffect;
        private bool radioEncryptionEffect;

        private bool irlRadioRXInterference = false;

        private readonly SyncedServerSettings serverSettings;

        public ClientEffectsPipeline()
        {
            profileSettings = GlobalSettingsStore.Instance.ProfileSettingsStore;
            serverSettings = SyncedServerSettings.Instance;

            _fxProviders.Add(CachedAudioEffect.AudioEffectTypes.NATO_TONE, new VolumeCachedEffectProvider(new CachedEffectProvider(effectProvider.NATOTone)));
            _fxProviders.Add(CachedAudioEffect.AudioEffectTypes.HAVEQUICK_TONE, new VolumeCachedEffectProvider(new CachedEffectProvider(effectProvider.HAVEQUICKTone)));
            _fxProviders.Add(CachedAudioEffect.AudioEffectTypes.UHF_NOISE, new VolumeCachedEffectProvider(new CachedEffectProvider(effectProvider.UHFNoise)));
            _fxProviders.Add(CachedAudioEffect.AudioEffectTypes.VHF_NOISE, new VolumeCachedEffectProvider(new CachedEffectProvider(effectProvider.VHFNoise)));
            _fxProviders.Add(CachedAudioEffect.AudioEffectTypes.HF_NOISE, new VolumeCachedEffectProvider(new CachedEffectProvider(effectProvider.HFNoise)));
            _fxProviders.Add(CachedAudioEffect.AudioEffectTypes.FM_NOISE, new VolumeCachedEffectProvider(new CachedEffectProvider(effectProvider.FMNoise)));
            _fxProviders.Add(CachedAudioEffect.AudioEffectTypes.AM_COLLISION, new VolumeCachedEffectProvider(new CachedEffectProvider(effectProvider.AMCollision)));

            _highPassFilter = BiQuadFilter.HighPassFilter(Constants.OUTPUT_SAMPLE_RATE, 520, 0.97f);
            _lowPassFilter = BiQuadFilter.LowPassFilter(Constants.OUTPUT_SAMPLE_RATE, 4130, 2.0f);
            RefreshSettings();
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

                _fxProviders[CachedAudioEffect.AudioEffectTypes.UHF_NOISE].Volume = profileSettings.GetClientSettingFloat(ProfileSettingsKeys.UHFNoiseVolume);
                _fxProviders[CachedAudioEffect.AudioEffectTypes.VHF_NOISE].Volume = profileSettings.GetClientSettingFloat(ProfileSettingsKeys.VHFNoiseVolume);
                _fxProviders[CachedAudioEffect.AudioEffectTypes.HF_NOISE].Volume = profileSettings.GetClientSettingFloat(ProfileSettingsKeys.HFNoiseVolume);
                _fxProviders[CachedAudioEffect.AudioEffectTypes.FM_NOISE].Volume = profileSettings.GetClientSettingFloat(ProfileSettingsKeys.FMNoiseVolume);

                radioEffects = profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioEffects);

                radioBackgroundNoiseEffect = profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioBackgroundNoiseEffect);
                radioEncryptionEffect = profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioEncryptionEffects);

                irlRadioRXInterference = serverSettings.GetSettingAsBool(ServerSettingsKeys.IRL_RADIO_RX_INTERFERENCE);
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
            if (!transmission.NoAudioEffects)
            {
                if (transmission.Modulation == Modulation.MIDS
                    || transmission.Modulation == Modulation.SATCOM
                    || transmission.Modulation == Modulation.INTERCOM)
                {
                    if (radioEffects)
                    {
                        AddRadioEffectIntercom(buffer, count, offset, transmission.Modulation);
                    }
                }
                else
                {
                    AddRadioEffect(buffer, count, offset, transmission.Modulation, transmission.Frequency, transmission.Encryption);
                }
            }

            //final adjust
            AdjustVolume(buffer, count, offset, transmission.Volume);

            return buffer;
        }

        private void AdjustVolume(float[] buffer, int count, int offset, float volume)
        {
            int outputIndex = offset;
            while (outputIndex < offset + count)
            {
                buffer[outputIndex] *= volume;

                outputIndex++;
            }
        }

        private void AddRadioEffectIntercom(float[] buffer, int count, int offset, Modulation modulation)
        {
            int outputIndex = offset;
            while (outputIndex < offset + count)
            {
                var audio = _highPassFilter.Transform(buffer[outputIndex]);

                audio = _highPassFilter.Transform(audio);

                if (float.IsNaN(audio))
                    audio = _lowPassFilter.Transform(buffer[outputIndex]);
                else
                    audio = _lowPassFilter.Transform(audio);

                if (!float.IsNaN(audio))
                {
                    // clip
                    if (audio > 1.0f)
                        audio = 1.0f;
                    if (audio < -1.0f)
                        audio = -1.0f;

                    buffer[outputIndex] = audio;
                }

                outputIndex++;
            }
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

        private ISampleProvider GetNoiseProvider(Modulation modulation, double freq)
        {
            switch (modulation)
            {
                case Modulation.AM:
                case Modulation.HAVEQUICK:
                    if (freq > 200e6) // UHF range
                    {
                        return _fxProviders[CachedAudioEffect.AudioEffectTypes.UHF_NOISE];
                    }

                    if (freq > 80e6)
                    {
                        return _fxProviders[CachedAudioEffect.AudioEffectTypes.VHF_NOISE];
                    }

                    return _fxProviders[CachedAudioEffect.AudioEffectTypes.HF_NOISE];
                case Modulation.FM:
                case Modulation.SINCGARS:
                    return _fxProviders[CachedAudioEffect.AudioEffectTypes.FM_NOISE];
            }

            return null;
        }

        private struct Compressor
        {
            public float Attack;
            public float MakeUp;
            public float Release;
            public float Slope;
            public float Threshold;
        };

        private struct Saturation
        {
            public float Gain;
            public float Threshold;
        }

        private class Radio
        {
            static public readonly float SAMPLE_RATE = Constants.OUTPUT_SAMPLE_RATE;
            public Dsp.IFilter[] PrepassFilters { get; set; }
            public Dsp.IFilter[] PostCompressorFilters { get; set; }
            public Dsp.IFilter[] ReceiverFilters { get; set; }
            public Compressor Compressor { get; set; }
            public Saturation Saturation { get; set; }

            public float NoiseGain { get; set; }
            public float PostGain { get; set; }
        };

#if true
        private static readonly Radio Arc210 = new Radio()
        {
            PrepassFilters = new Dsp.IFilter[]
            {
                new Dsp.BiQuadFilter()
                    {
                        Filter = BiQuadFilter.HighPassFilter(Constants.OUTPUT_SAMPLE_RATE, 1700, 0.53f),
                    },
                new Dsp.BiQuadFilter()
                    {
                        Filter = BiQuadFilter.PeakingEQ(Constants.OUTPUT_SAMPLE_RATE, 2801, 0.5f, 5f),
                    },

                Dsp.FirstOrderFilter.LowPass(Constants.OUTPUT_SAMPLE_RATE, 5538),
            },

            PostCompressorFilters = new []
            {
                new Dsp.BiQuadFilter()
                    {
                        Filter = BiQuadFilter.HighPassFilter(Constants.OUTPUT_SAMPLE_RATE, 456, 0.36f),
                    },
                new Dsp.BiQuadFilter()
                    {
                        Filter = BiQuadFilter.LowPassFilter(Constants.OUTPUT_SAMPLE_RATE, 5435, 0.39f)
                    }
            },

            ReceiverFilters = new[]
            {
                Dsp.FirstOrderFilter.HighPass(Constants.OUTPUT_SAMPLE_RATE, 270),
                Dsp.FirstOrderFilter.LowPass(Constants.OUTPUT_SAMPLE_RATE, 4500)
            },

            Compressor = new Compressor
            {
                Attack = 0.01f,
                MakeUp = 6,
                Release = 0.2f,
                Threshold = -33,
                Slope = 0.85f
            },

            Saturation = new Saturation
            {
                Gain = 9,
                Threshold = -23,
            },

            NoiseGain = -33,
            PostGain = 12,
        };
#endif

#if true
        private static readonly Radio Arc164 = new Radio()
        {
            
            PrepassFilters = new[]
            {
                new Dsp.BiQuadFilter()
                    {
                        Filter = BiQuadFilter.HighPassFilter(Radio.SAMPLE_RATE, 954, 0.09f),
                    },
                new Dsp.BiQuadFilter()
                    {
                        Filter = BiQuadFilter.PeakingEQ(Radio.SAMPLE_RATE, 2302, 0.63f, 13f),
                    },
                new Dsp.BiQuadFilter()
                    {
                        Filter = BiQuadFilter.LowPassFilter(Radio.SAMPLE_RATE, 5165, 0.4f)
                    }
            },

            PostCompressorFilters = new Dsp.IFilter[]
            {
                Dsp.FirstOrderFilter.HighPass(Radio.SAMPLE_RATE, 829),
                new Dsp.BiQuadFilter()
                    {
                        Filter = BiQuadFilter.LowPassFilter(Radio.SAMPLE_RATE, 5435, 0.1f),
                    }
            },

            ReceiverFilters = new[]
            {
                Dsp.FirstOrderFilter.HighPass(Radio.SAMPLE_RATE, 270),
                Dsp.FirstOrderFilter.LowPass(Radio.SAMPLE_RATE, 4500)
            },

            Compressor = new Compressor
            {
                Attack = 0.01f,
                MakeUp = 5,
                Release = 0.2f,
                Threshold = -35,
                Slope = 0.38f
            },

            Saturation = new Saturation
            {
                Gain = 11,
                Threshold = -30,
            },

            NoiseGain = -19,
            PostGain = -4,
        };
#endif
#if false
        private static readonly Radio Arc222 = new Radio()
        {
            PrepassFilters = new[]
            {
                new BiQuadProvider.Pass {
                    Filter = BiQuadFilter.HighPassFilter(Constants.OUTPUT_SAMPLE_RATE, 1700, 0.53f),
                },
                new BiQuadProvider.Pass {
                    Filter = BiQuadFilter.BandPassFilterConstantSkirtGain(Constants.OUTPUT_SAMPLE_RATE, 2801, 0.5f),
                    GainLinear = (float)Decibels.DecibelsToLinear(5f),
                },
                new BiQuadProvider.Pass {
                    Filter = BiQuadFilter.LowPassFilter(Constants.OUTPUT_SAMPLE_RATE, 5538, 0.05f),
                },
            },

            PostCompressorFilters = new[]
            {
                new BiQuadProvider.Pass {
                    Filter = BiQuadFilter.HighPassFilter(Constants.OUTPUT_SAMPLE_RATE, 456, 0.36f),
                },
                new BiQuadProvider.Pass {
                    Filter = BiQuadFilter.LowPassFilter(Constants.OUTPUT_SAMPLE_RATE, 5435, 0.39f),
                },
            },

            ReceiverFilters = new[]
            {
                new BiQuadProvider.Pass {
                    Filter = BiQuadFilter.HighPassFilter(Constants.OUTPUT_SAMPLE_RATE, 270, 0.707f),
                },
                new BiQuadProvider.Pass {
                    Filter = BiQuadFilter.LowPassFilter(Constants.OUTPUT_SAMPLE_RATE, 4500, 0.707f),
                },
            },

            Compressor = new Compressor
            {
                Attack = 0.01f,
                MakeUp = 6,
                Release = 0.2f,
                Threshold = -33,
                Slope = 0.85f
            },

            NoiseGain = -36
        };
#endif
        private void AddRadioEffect(float[] buffer, int count, int offset, Modulation modulation, double freq, short encryption)
        {
            // NAudio version.
            // Chain of effects being applied.
            // TODO: We should be able to precompute a lot of this.
            ISampleProvider voiceProvider = new TransmissionProvider(buffer, offset);

            var radioModel = Arc210;
            if (radioEffectsEnabled)
            {
                // Variant mirroring the settings from DCS' ARC210 definition.
                // prepass.
                var sampleRate = voiceProvider.WaveFormat.SampleRate;

                if (clippingEnabled)
                {
                    voiceProvider = new ClippingProvider(voiceProvider, RadioFilter.CLIPPING_MIN, RadioFilter.CLIPPING_MAX);
                }


                voiceProvider = new BiQuadProvider(voiceProvider)
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
                    Ratio =  1f / radioModel.Compressor.Slope,
                };

                // post
                voiceProvider = new BiQuadProvider(voiceProvider)
                {
                    Filters = radioModel.PostCompressorFilters
                };

                // Bump gain.
                voiceProvider = new VolumeSampleProvider(voiceProvider)
                {
                    Volume = (float)Decibels.DecibelsToLinear(radioModel.PostGain)
                };

                voiceProvider = new MixingSampleProvider(new ISampleProvider[]
                {
                    voiceProvider,
#if false
                    new GaussianWhiteNoise(sampleRate, 1)
                    {
                        Gain = (float)Decibels.DecibelsToLinear(radioModel.NoiseGain) * 1.1561e-06f
                    },
#endif
                });

                var encryptionEffects = radioEncryptionEffect && encryption > 0;
                if (encryptionEffects)
                {
                    voiceProvider = new CVSDProvider(voiceProvider);
                }

                // Add receiver bandpass.

                voiceProvider = new BiQuadProvider(voiceProvider)
                {
                    Filters = radioModel.ReceiverFilters
                };
            }



            // Mix in the noise, tones, etc.
            // Note that they are applied LIFO.
            var fxMixer = new MixingSampleProvider(voiceProvider.WaveFormat);

            if (radioBackgroundNoiseEffect)
            {
                var noise = GetNoiseProvider(modulation, freq);
                if (noise != null)
                {
                    fxMixer.AddMixerInput(new VolumeSampleProvider(noise)
                    {
                        Volume = (float)Decibels.DecibelsToLinear(radioModel.NoiseGain)
                    });
                }
            }

            // Modulation tone, if applicable.
            {
                var tone = GetToneProvider(modulation);
                if (tone != null && tone.Active)
                {
                    fxMixer.AddMixerInput(tone);
                }
            }

            // And now the voice.
            fxMixer.AddMixerInput(voiceProvider);

            voiceProvider = new ClippingProvider(fxMixer, -1, 1);

            // Apply the post processing to the voice!
            voiceProvider.Read(buffer, offset, count);
        }
    }
}