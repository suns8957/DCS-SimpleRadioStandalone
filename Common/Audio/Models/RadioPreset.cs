using System.Text.Json.Serialization;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models
{
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
}
