using System.Collections.Concurrent;
using System.IO;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.NAudioLame;
using NAudio.Wave;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Recording;

public class AudioRecordingFrequencyGroup
{
    private readonly double _frequency;
    private readonly string _sessionId;
    private readonly WaveFormat _waveFormat;
    private WaveBuffer _buffer;

    private LameMP3FileWriter _waveWriter;

    public AudioRecordingFrequencyGroup(double frequency, string sessionId, WaveFormat format)
    {
        _frequency = frequency;
        _sessionId = sessionId;
        _waveFormat = format;
        //Sample rate x 3 (seconds) x 4 (we put in floats but want out bytes)
        _buffer = new WaveBuffer(Constants.OUTPUT_SAMPLE_RATE * 3 * 4);
    }

    public ConcurrentDictionary<string, ClientRecordedAudioProvider> RecordedAudioProvider { get; } = new();

    public void ProcessClientAudio(long elapsedTime)
    {
        if (_waveWriter == null)
            _waveWriter =
                new LameMP3FileWriter($"Recordings{Path.DirectorySeparatorChar}{_sessionId}-{_frequency}.mp3",
                    _waveFormat, 128);

        var samplesRequired = (int)elapsedTime * (_waveFormat.SampleRate / 1000);

        if (samplesRequired * 4 > _buffer.MaxSize) _buffer = new WaveBuffer(samplesRequired * 4);

        var read = 0;

        foreach (var client in RecordedAudioProvider) read = client.Value.Read(_buffer, 0, samplesRequired);

        _waveWriter.Write(_buffer, 0, samplesRequired * 4);
        _buffer.Clear();
    }

    public void AddClientAudio(ClientAudio audio)
    {
        //sort out effects!
        //16bit PCM Audio
        // If we have recieved audio, create a new buffered audio and read it
        ClientRecordedAudioProvider client = null;
        if (RecordedAudioProvider.TryGetValue(audio.OriginalClientGuid, out var value))
        {
            client = value;
        }
        else
        {
            client = new ClientRecordedAudioProvider(
                WaveFormat.CreateIeeeFloatWaveFormat(Constants.OUTPUT_SAMPLE_RATE, 1));
            RecordedAudioProvider[audio.OriginalClientGuid] = client;
        }

        // process the audio samples
        // We have them in a list - and each client will return the requested number of floats - and generate dead air
        // as appropriate
        client.AddClientAudioSamples(audio);
    }

    public void RemoveClient(string guid)
    {
        RecordedAudioProvider.TryRemove(guid, out var ignore);
    }

    public void Stop()
    {
        _waveWriter?.Close();
        _waveWriter = null;
    }
}