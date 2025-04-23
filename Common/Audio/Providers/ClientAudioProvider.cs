using System;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Client;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Models;
using NAudio.Wave;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers;

public class ClientAudioProvider : AudioProvider
{
    //   private readonly WaveFileWriter waveWriter;
    public ClientAudioProvider(bool passThrough = false) : base(passThrough)
    {
        if (!passThrough)
        {
            var radios = Constants.MAX_RADIOS;
            JitterBufferProviderInterface =
                new JitterBufferProviderInterface[radios];

            for (var i = 0; i < radios; i++)
                JitterBufferProviderInterface[i] =
                    new JitterBufferProviderInterface(new WaveFormat(Constants.OUTPUT_SAMPLE_RATE, 1));
        }
        //    waveWriter = new NAudio.Wave.WaveFileWriter($@"C:\\temp\\output{RandomFloat()}.wav", new WaveFormat(Constants.OUTPUT_SAMPLE_RATE, 1));
    }

    public JitterBufferProviderInterface[] JitterBufferProviderInterface { get; }

    public override JitterBufferAudio AddClientAudioSamples(ClientAudio audio)
    {
        //sort out volume
        //            var timer = new Stopwatch();
        //            timer.Start();

        var newTransmission = LikelyNewTransmission();

        //TODO reduce the size of this buffer
        var decoded = _decoder.DecodeFloat(audio.EncodedAudio,
            audio.EncodedAudio.Length, out var decodedLength, newTransmission);

        if (decodedLength <= 0)
        {
            Logger.Info("Failed to decode audio from Packet for client");
            return null;
        }

        // for some reason if this is removed then it lags?!
        //guess it makes a giant buffer and only uses a little?
        //Answer: makes a buffer of 4000 bytes - so throw away most of it

        //TODO reuse this buffer
        var tmp = new float[decodedLength / 4];
        Buffer.BlockCopy(decoded, 0, tmp, 0, decodedLength);

        //convert the byte buffer to a wave buffer
        //   var waveBuffer = new WaveBuffer(tmp);

        // waveWriter.WriteSamples(tmp,0,tmp.Length);

        audio.PcmAudioFloat = tmp;

        if (newTransmission)
        {
            // System.Diagnostics.Debug.WriteLine(audio.ClientGuid+"ADDED");
            //append ms of silence - this functions as our jitter buffer??
            var silencePad = Constants.OUTPUT_SAMPLE_RATE / 1000 * SILENCE_PAD;
            var newAudio = new float[audio.PcmAudioFloat.Length + silencePad];
            Buffer.BlockCopy(audio.PcmAudioFloat, 0, newAudio, silencePad, audio.PcmAudioFloat.Length);
            audio.PcmAudioFloat = newAudio;
        }

        LastUpdate = DateTime.Now.Ticks;

        if (passThrough)
            //return MONO PCM 16 as bytes
            return new JitterBufferAudio
            {
                Audio = audio.PcmAudioFloat,
                PacketNumber = audio.PacketNumber,
                Modulation = (Modulation)audio.Modulation,
                ReceivedRadio = audio.ReceivedRadio,
                Volume = audio.Volume,
                IsSecondary = audio.IsSecondary,
                Frequency = audio.Frequency,
                Guid = audio.ClientGuid,
                OriginalClientGuid = audio.OriginalClientGuid
                //TODO fix this
                //comapre with ClientAudioProvider
            };

        JitterBufferProviderInterface[audio.ReceivedRadio].AddSamples(new JitterBufferAudio
        {
            Audio = audio.PcmAudioFloat,
            PacketNumber = audio.PacketNumber,
            Modulation = (Modulation)audio.Modulation,
            ReceivedRadio = audio.ReceivedRadio,
            Volume = audio.Volume,
            IsSecondary = audio.IsSecondary,
            Frequency = audio.Frequency,
            Guid = audio.ClientGuid,
            OriginalClientGuid = audio.OriginalClientGuid
            //TODO fix this
            //comapre with ClientAudioProvider
        });

        return null;

        //timer.Stop();
    }
}