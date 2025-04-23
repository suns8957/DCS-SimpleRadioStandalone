using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Speech.AudioFormat;
using System.Speech.Synthesis;
using Ciribob.FS3D.SimpleRadio.Standalone.Common.Audio.Opus;
using Ciribob.FS3D.SimpleRadio.Standalone.Common.Audio.Opus.Core;
using Ciribob.FS3D.SimpleRadio.Standalone.ExternalAudioClient.Client;
using Google.Cloud.TextToSpeech.V1;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NLog;

namespace Ciribob.FS3D.SimpleRadio.Standalone.ExternalAudioClient.Audio;

public class AudioGenerator
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public static readonly int INPUT_SAMPLE_RATE = 16000;
    public static readonly int INPUT_AUDIO_LENGTH_MS = 40;
    public static readonly int TTS_SILENCE_PADDING = 60; //how many frames to pad the Text to Speech

    public static readonly int
        SEGMENT_FRAMES =
            INPUT_SAMPLE_RATE / 1000 * INPUT_AUDIO_LENGTH_MS; //640 is 40ms as INPUT_SAMPLE_RATE / 1000 *40 = 640

    private readonly Program.Options opts;

    public AudioGenerator(Program.Options opts)
    {
        this.opts = opts;

        if (opts.Gender.Trim().ToLower() == "male")
            SpeakerGender = VoiceGender.Male;
        else if (opts.Gender.Trim().ToLower() == "neutral")
            SpeakerGender = VoiceGender.Neutral;
        else
            SpeakerGender = VoiceGender.Female;
    }

    public VoiceGender SpeakerGender { get; set; }

    private byte[] GoogleTTS(string msg)
    {
        try
        {
            var builder = new TextToSpeechClientBuilder
            {
                CredentialsPath = opts.GoogleCredentials
            };

            var client = builder.Build();

            SynthesisInput input;

            if (opts.SSML)
                input = new SynthesisInput
                {
                    Ssml = msg
                };
            else
                input = new SynthesisInput
                {
                    Text = msg
                };

            VoiceSelectionParams voice = null;

            if (!string.IsNullOrEmpty(opts.Voice))
            {
                voice = new VoiceSelectionParams
                {
                    Name = opts.Voice,
                    LanguageCode = opts.Voice.Substring(0, 5)
                };
            }
            else
            {
                voice = new VoiceSelectionParams
                {
                    LanguageCode = opts.Culture
                };

                switch (opts.Gender.ToLowerInvariant().Trim())
                {
                    case "male":
                        voice.SsmlGender = SsmlVoiceGender.Male;
                        break;
                    case "neutral":
                        voice.SsmlGender = SsmlVoiceGender.Neutral;
                        break;
                    case "female":
                        voice.SsmlGender = SsmlVoiceGender.Female;
                        break;
                    default:
                        voice.SsmlGender = SsmlVoiceGender.Male;
                        break;
                }
            }

            var config = new AudioConfig
            {
                AudioEncoding = AudioEncoding.Linear16,
                SampleRateHertz = INPUT_SAMPLE_RATE
            };

            var response = client.SynthesizeSpeech(new SynthesizeSpeechRequest
            {
                Input = input,
                Voice = voice,
                AudioConfig = config
            });

            var tempFile = Path.GetTempFileName();

            using (var stream = File.Create(tempFile))
            {
                response.AudioContent.WriteTo(stream);
            }

            byte[] bytes = null;
            using (var reader = new WaveFileReader(tempFile))
            {
                bytes = new byte[reader.Length];
                var read = reader.Read(bytes, 0, bytes.Length);
                Logger.Info($"Success with Google TTS - read {read} bytes");
            }

            //cleanup
            File.Delete(tempFile);

            //append 2000ms to the start
            var audioBytes = bytes;
            var extended = new byte[audioBytes.Length + SEGMENT_FRAMES * 2 * TTS_SILENCE_PADDING];

            Buffer.BlockCopy(audioBytes, 0, extended, SEGMENT_FRAMES * 2 * TTS_SILENCE_PADDING, audioBytes.Length);

            return extended;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Error with Google Text to Speech: {ex.Message}");
        }

        return new byte[0];
    }


    private byte[] LocalTTS(string msg)
    {
        try
        {
            using (var synth = new SpeechSynthesizer())
            using (var stream = new MemoryStream())
            {
                if (opts.Voice == null || opts.Voice.Length == 0)
                {
                    if (opts.Culture == null)
                        synth.SelectVoiceByHints(SpeakerGender, VoiceAge.Adult);
                    else
                        synth.SelectVoiceByHints(SpeakerGender, VoiceAge.Adult, 0,
                            new CultureInfo(opts.Culture, false));
                }
                else
                {
                    synth.SelectVoice(opts.Voice);
                }

                synth.Rate = opts.speed;

                var intVol = (int)(opts.Volume * 100.0);

                if (intVol > 100) intVol = 100;

                synth.Volume = intVol;

                synth.SetOutputToAudioStream(stream,
                    new SpeechAudioFormatInfo(INPUT_SAMPLE_RATE, AudioBitsPerSample.Sixteen, AudioChannel.Mono));

                synth.Speak(msg);

                //append 2000ms to the start
                var audioBytes = stream.ToArray();
                var extended = new byte[audioBytes.Length + SEGMENT_FRAMES * 2 * TTS_SILENCE_PADDING];

                Buffer.BlockCopy(audioBytes, 0, extended, SEGMENT_FRAMES * 2 * TTS_SILENCE_PADDING, audioBytes.Length);

                return extended;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Error with Microsoft Text to Speech: {ex.Message}");
        }

        return new byte[0];
    }

    private IWaveProvider GetMP3WaveProvider()
    {
        Logger.Info($"Reading MP3 @ {opts.File}");

        var mp3Reader = new Mp3FileReader(opts.File);
        var bytes = (int)mp3Reader.Length;
        var buffer = new byte[bytes];

        Logger.Info($"Read MP3 @ {mp3Reader.WaveFormat.SampleRate}");

        if (mp3Reader.WaveFormat.SampleRate < INPUT_SAMPLE_RATE)
        {
            Logger.Error(
                $"MP3 Sample rate must be at least 16000 but is {mp3Reader.WaveFormat.SampleRate} - Quitting. Use Audacity or another tool to resample as 16000 or Higher");
            Environment.Exit(1);
        }

        var read = mp3Reader.Read(buffer, 0, bytes);
        var bufferedWaveProvider = new BufferedWaveProvider(mp3Reader.WaveFormat)
        {
            BufferLength = read * 2, ReadFully = false, DiscardOnBufferOverflow = true
        };

        bufferedWaveProvider.AddSamples(buffer, 0, read);
        var volumeSample =
            new VolumeSampleProvider(bufferedWaveProvider.ToSampleProvider()) { Volume = opts.Volume };

        mp3Reader.Close();
        mp3Reader.Dispose();

        Logger.Info("Convert to Mono 16bit PCM");

        //after this we've got 16 bit PCM Mono  - just need to sort sample rate
        return volumeSample.ToMono().ToWaveProvider16();
    }

    private byte[] GetMP3Bytes()
    {
        var resampledBytesList = new List<byte>();
        var waveProvider = GetMP3WaveProvider();

        Logger.Info($"Convert to Mono 16bit PCM 16000KHz from {waveProvider.WaveFormat}");
        //loop thorough in up to 1 second chunks
        var resample = new EventDrivenResampler(waveProvider.WaveFormat, new WaveFormat(INPUT_SAMPLE_RATE, 1));

        var buffer = new byte[waveProvider.WaveFormat.AverageBytesPerSecond * 2];

        var read = 0;
        while ((read = waveProvider.Read(buffer, 0, waveProvider.WaveFormat.AverageBytesPerSecond)) > 0)
            //resample as we go
            resampledBytesList.AddRange(resample.ResampleBytes(buffer, read));

        Logger.Info($"Converted to Mono 16bit PCM 16000KHz from {waveProvider.WaveFormat}");

        return resampledBytesList.ToArray();
    }

    public List<byte[]> GetOpusBytes()
    {
        var opusBytes = new List<byte[]>();

        byte[] resampledBytes;

        if (opts.File != null && opts.File.ToLowerInvariant().EndsWith(".mp3"))
        {
            Logger.Info("Reading MP3 it looks like a file");
            resampledBytes = GetMP3Bytes();
        }
        else
        {
            Logger.Info("Doing Text To Speech as its not an MP3 path");

            var msg = opts.Text;
            if (opts.TextFile != null)
            {
                Logger.Info($"Reading text in file from path: {opts.TextFile}");
                msg = File.ReadAllText(opts.TextFile);
            }

            if (!string.IsNullOrEmpty(opts.GoogleCredentials))
                resampledBytes = GoogleTTS(msg);
            else
                //TODO fix gender
                resampledBytes = LocalTTS(msg);
        }

        Logger.Info("Encode as Opus");
        var encoder = OpusEncoder.Create(INPUT_SAMPLE_RATE, 1, Application.Voip);

        var pos = 0;
        while (pos + SEGMENT_FRAMES * 2 < resampledBytes.Length)
        {
            var buf = new byte[SEGMENT_FRAMES * 2];
            Buffer.BlockCopy(resampledBytes, pos, buf, 0, SEGMENT_FRAMES * 2);

            var outLength = 0;
            var frame = encoder.Encode(buf, buf.Length, out outLength);

            if (outLength > 0)
            {
                //create copy with small buffer
                var encoded = new byte[outLength];

                Buffer.BlockCopy(frame, 0, encoded, 0, outLength);

                opusBytes.Add(encoded);
            }

            pos += SEGMENT_FRAMES * 2;
        }

        if (pos + 1 < resampledBytes.Length)
        {
            //last bit - less than 40 ms
            var buf = new byte[SEGMENT_FRAMES * 2];
            Buffer.BlockCopy(resampledBytes, pos, buf, 0, resampledBytes.Length - pos);

            var outLength = 0;
            var frame = encoder.Encode(buf, buf.Length, out outLength);

            if (outLength > 0)
            {
                //create copy with small buffer
                var encoded = new byte[outLength];

                Buffer.BlockCopy(frame, 0, encoded, 0, outLength);

                opusBytes.Add(encoded);
            }
        }

        encoder.Dispose();
        Logger.Info("Finished encoding as Opus");

        return opusBytes;
    }
}