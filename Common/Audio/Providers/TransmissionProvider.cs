using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers
{
    internal class TransmissionProvider : ISampleProvider
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
}
