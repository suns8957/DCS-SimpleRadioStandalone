using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers;
using NAudio.Dsp;
using NAudio.Utils;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Text.Json.Serialization;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models
{
    namespace Dto
    {
        [JsonDerivedType(typeof(ChainEffect), typeDiscriminator: "chain")]
        [JsonDerivedType(typeof(FiltersEffect), typeDiscriminator: "filters")]

        [JsonDerivedType(typeof(SaturationEffect), typeDiscriminator: "saturation")]
        [JsonDerivedType(typeof(CompressorEffect), typeDiscriminator: "compressor")]
        [JsonDerivedType(typeof(GainEffect), typeDiscriminator: "gain")]

        [JsonDerivedType(typeof(CVSDEffect), typeDiscriminator: "cvsd")]
        internal abstract class IEffect
        {
            public abstract ISampleProvider ToSampleProvider(ISampleProvider source);
        };

        internal class CVSDEffect : IEffect
        {
            public override ISampleProvider ToSampleProvider(ISampleProvider source)
            {
                return new CVSDProvider(source);
            }
        };

        internal class ChainEffect : IEffect
        {
            public IEffect[] Effects { get; set; }
            public override ISampleProvider ToSampleProvider(ISampleProvider source)
            {
                var last = source;
                foreach (var effect in Effects)
                {
                    last = effect.ToSampleProvider(last);
                }
                return last;
            }
        };

        internal class FiltersEffect : IEffect
        {
            public Dsp.IFilter[] Filters { get; set; }
            public override ISampleProvider ToSampleProvider(ISampleProvider source)
            {
                return new FiltersProvider(source)
                {
                    Filters = Filters
                };
            }
        };

        internal class SaturationEffect : IEffect
        {
            public float Gain { get; set; }
            public float Threshold { get; set; }

            public override ISampleProvider ToSampleProvider(ISampleProvider source)
            {
                return new SaturationProvider(source)
                {
                    GainDB = Gain,
                    ThresholdDB = Threshold
                };
            }
        };

        internal class CompressorEffect : IEffect
        {
            public float Attack { get; set; }
            public float MakeUp { get; set; }
            public float Release { get; set; }
            public float Threshold { get; set; }
            public float Slope { get; set; }

            public override ISampleProvider ToSampleProvider(ISampleProvider source)
            {
                return new SimpleCompressorEffect(source)
                {
                    Attack = Attack,
                    MakeUpGain = MakeUp,
                    Release = Release,
                    Threshold = Threshold,
                    Ratio = 1f / Slope,
                    Enabled = true,
                };
            }
        };

        internal class GainEffect : IEffect
        {
            public float Gain {  get; set; }

            public override ISampleProvider ToSampleProvider(ISampleProvider source)
            {
                return new VolumeSampleProvider(source)
                {
                    Volume = (float)Decibels.DecibelsToLinear(Gain),
                };
            }
        };

        internal class RadioPreset
        {
            public int Version { get; set; }
            public float NoiseGain { get; set; }
            public IEffect TxEffect { get; set; }
            public IEffect RxEffect { get; set; }
            public IEffect EncryptionEffect { get; set; }

        };
    }
    internal struct Compressor
    {
        [JsonInclude, JsonRequired]
        public float Attack;
        [JsonInclude, JsonRequired]
        public float MakeUp;
        [JsonInclude, JsonRequired]
        public float Release;
        [JsonInclude, JsonRequired]
        public float Slope;
        [JsonInclude, JsonRequired]
        public float Threshold;
    };

    internal struct Saturation
    {
        [JsonInclude]
        public float Gain;

        [JsonInclude, JsonRequired]
        public float Threshold;
    }
    internal class RadioPreset
    {
        public Dsp.IFilter[] PrepassFilters { get; set; }
        public Dsp.IFilter[] PostCompressorFilters { get; set; }
        public Dsp.IFilter[] ReceiverFilters { get; set; }
        public Compressor Compressor { get; set; }
        public Saturation Saturation { get; set; }

        public float NoiseGain { get; set; }
        public float PostGain { get; set; }
        public float InnerNoise { get; set; }
    };

    internal class DefaultRadioPresets
    {
        // ARC-210 as default radio FX.
        public static readonly RadioPreset Arc210 = new RadioPreset()
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

            PostCompressorFilters = new[]
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
            InnerNoise = 1.1561e-06f,
        };

        public static readonly RadioPreset Intercom = new RadioPreset()
        {
            PrepassFilters = new Dsp.IFilter[]
            {
                new Dsp.BiQuadFilter()
                    {
                        Filter = BiQuadFilter.HighPassFilter(Constants.OUTPUT_SAMPLE_RATE, 207, 0.5f),
                    },
                new Dsp.BiQuadFilter()
                    {
                        Filter = BiQuadFilter.PeakingEQ(Constants.OUTPUT_SAMPLE_RATE, 3112, 0.4f, 16f),
                    },
                new Dsp.BiQuadFilter()
                    {
                    Filter = BiQuadFilter.LowPassFilter(Constants.OUTPUT_SAMPLE_RATE, 6036, 0.4f),
                    },

                Dsp.FirstOrderFilter.LowPass(Constants.OUTPUT_SAMPLE_RATE, 5538),
            },

            PostCompressorFilters = new[]
            {
                new Dsp.BiQuadFilter()
                    {
                        Filter = BiQuadFilter.HighPassFilter(Constants.OUTPUT_SAMPLE_RATE, 393, 0.43f),
                    },
                new Dsp.BiQuadFilter()
                    {
                        Filter = BiQuadFilter.LowPassFilter(Constants.OUTPUT_SAMPLE_RATE, 4875, 0.3f)
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
                MakeUp = -1,
                Release = 0.2f,
                Threshold = -17,
                Slope = 0.85f
            },

            Saturation = new Saturation
            {
                Gain = 2,
                Threshold = -33,
            },

            NoiseGain = -60,
            PostGain = 8,
        };
    };
    

}
