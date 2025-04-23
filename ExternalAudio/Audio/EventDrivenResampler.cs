using System;
using NAudio.Dmo;
using NAudio.Dsp;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Ciribob.FS3D.SimpleRadio.Standalone.ExternalAudioClient.Audio
{
    public class EventDrivenResampler
    {
        private readonly bool windowsN;
        private readonly BufferedWaveProvider buf;
        private ResamplerDmoStream dmoResampler;

        private WaveFormat input;
        private readonly WdlResamplingSampleProvider mediaFoundationResampler;
        private WaveFormat output;
        private WdlResampler resampler;
        private readonly IWaveProvider waveOut;

        public EventDrivenResampler(WaveFormat input, WaveFormat output)
        {
            windowsN = DetectWindowsN();
            this.input = input;
            this.output = output;
            buf = new BufferedWaveProvider(input);
            buf.ReadFully = false;

            if (windowsN)
            {
                mediaFoundationResampler = new WdlResamplingSampleProvider(buf.ToSampleProvider(), output.SampleRate);
                waveOut = mediaFoundationResampler.ToMono().ToWaveProvider16();
            }
            else
            {
                dmoResampler = new ResamplerDmoStream(buf, output);
            }
        }

        private bool DetectWindowsN()
        {
            try
            {
                var dmoResampler = new DmoResampler();
                dmoResampler.Dispose();
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }

        private byte[] ResampleBytesDMO(byte[] inputByteArray, int length)
        {
            var outBuffer = new byte[length];
            buf.AddSamples(inputByteArray, 0, length);

            var read = dmoResampler.Read(outBuffer, 0, outBuffer.Length);

            if (read == 0)
            {
                return new byte[0];
            }

            var finalBuf = new byte[read];
            Buffer.BlockCopy(outBuffer, 0, finalBuf, 0, read);

            return finalBuf;
        }

        private byte[] ResampleBytesMFC(byte[] inputByteArray, int length)
        {
            var outBuffer = new byte[length];

            buf.AddSamples(inputByteArray, 0, length);

            var read = waveOut.Read(outBuffer, 0, outBuffer.Length);

            if (read == 0)
            {
                return new byte[0];
            }

            var finalBuf = new byte[read];
            Buffer.BlockCopy(outBuffer, 0, finalBuf, 0, read);

            return finalBuf;
        }

        public byte[] ResampleBytes(byte[] inputByteArray, int length)
        {
            if (windowsN)
                return ResampleBytesMFC(inputByteArray, length);
            return ResampleBytesDMO(inputByteArray, length);
        }

        private short[] ResampleDMO(byte[] inputByteArray, int length)
        {
            var bytes = ResampleBytes(inputByteArray, length);

            if (bytes.Length == 0) return new short[0];

            //convert byte to short
            var sdata = new short[bytes.Length / 2];
            Buffer.BlockCopy(bytes, 0, sdata, 0, bytes.Length);

            return sdata;
        }

        public short[] Resample(byte[] inputByteArray, int length)
        {
            if (windowsN)
                return ResampleMFC(inputByteArray, length);
            return ResampleDMO(inputByteArray, length);
        }

        private short[] ResampleMFC(byte[] inputByteArray, int length)
        {
            var outBuffer = new byte[length];

            buf.AddSamples(inputByteArray, 0, length);

            var read = waveOut.Read(outBuffer, 0, outBuffer.Length);

            if (read == 0)
            {
                return new short[0];
            }

            //convert byte to short
            var sdata = new short[read / 2];
            Buffer.BlockCopy(outBuffer, 0, sdata, 0, read);
            return sdata;
        }

        /// <summary>
        ///     Dispose
        /// </summary>
        /// <param name="disposing">True if disposing (not from finalizer)</param>
        public void Dispose(bool disposing)
        {
            buf.ClearBuffer();
            if (windowsN)
            {
                buf.ClearBuffer();
            }
            else
            {
                dmoResampler?.Dispose();
                dmoResampler = null;
            }
        }

        ~EventDrivenResampler()
        {
            Dispose(false);
        }
    }
}