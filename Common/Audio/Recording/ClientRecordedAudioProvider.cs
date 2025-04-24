using System;
using System.Collections.Generic;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Client;
using NAudio.Utils;
using NAudio.Wave;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Recording;

//mixes down a single clients audio to a single stream for output
public class ClientRecordedAudioProvider : AudioProvider, ISampleProvider
{
    private readonly JitterBufferProviderInterface _jitterBuffer;
    private readonly List<DeJitteredTransmission> _mainAudio = new();
    private readonly ClientEffectsPipeline pipeline = new();
    private float[] mixBuffer;

    public ClientRecordedAudioProvider(WaveFormat waveFormat) : base(false)
    {
        _jitterBuffer = new JitterBufferProviderInterface(new WaveFormat(waveFormat.SampleRate, waveFormat.Channels));

        if (waveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
            throw new ArgumentException("Mixer wave format must be IEEE float");

        WaveFormat = waveFormat;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        _mainAudio.Clear();
        var primarySamples = 0;

        var transmission = _jitterBuffer.Read(count);

        if (transmission.PCMAudioLength > 0)
        {
            //small optimisation - only do this if we need too
            mixBuffer = BufferHelpers.Ensure(mixBuffer, count);
            ClearArray(mixBuffer);

            _mainAudio.Add(transmission);
        }

        //merge all the audio for the client
        if (_mainAudio.Count > 0)
            mixBuffer = pipeline.ProcessClientTransmissions(mixBuffer, _mainAudio, out primarySamples);

        //Mix into the buffer
        for (var i = 0; i < primarySamples; i++) buffer[i + offset] += mixBuffer[i];

        //dont return full set
        return primarySamples;
    }

    public WaveFormat WaveFormat { get; }

    public float[] ClearArray(float[] buffer)
    {
        for (var i = 0; i < buffer.Length; i++) buffer[i] = 0;

        return buffer;
    }

    private int EnsureFullBuffer(float[] buffer, int samplesCount, int offset, int count)
    {
        // ensure we return a full buffer of STEREO
        if (samplesCount < count)
        {
            var outputIndex = offset + samplesCount;
            while (outputIndex < offset + count) buffer[outputIndex++] = 0;

            samplesCount = count;
        }

        //Should be impossible - ensures audio doesnt crash if its not
        if (samplesCount > count) samplesCount = count;

        return samplesCount;
    }

    public override JitterBufferAudio AddClientAudioSamples(ClientAudio audio)
    {
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

        _jitterBuffer.AddSamples(new JitterBufferAudio
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
        });

        return null;
    }
}