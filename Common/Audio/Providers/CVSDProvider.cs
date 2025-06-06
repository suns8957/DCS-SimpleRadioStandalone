using NAudio.Wave;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers
{
    internal class CVSDProvider : ISampleProvider
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
