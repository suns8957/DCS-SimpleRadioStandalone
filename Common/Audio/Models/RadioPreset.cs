namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models
{
    internal struct Compressor
    {
        public float Attack;
        public float MakeUp;
        public float Release;
        public float Slope;
        public float Threshold;
    };

    internal struct Saturation
    {
        public float Gain;
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
