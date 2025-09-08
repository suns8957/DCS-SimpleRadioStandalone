using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using NAudio.Dsp;
using NAudio.Wave;
using System;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers;

public class RadioFilter : ISampleProvider
{
    public static readonly float BOOST = 1.5f;
    public static readonly float CLIPPING_MAX = 4000 / 32768f;
    public static readonly float CLIPPING_MIN = -CLIPPING_MAX;

    private readonly BiQuadFilter[] _filters;
    //    private Stopwatch _stopwatch;

    private readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;
    private readonly ISampleProvider _source;

    public RadioFilter(ISampleProvider sampleProvider)
    {
        _source = sampleProvider;
        _filters = new BiQuadFilter[]
        {
            // ARC-210 prepass
            BiQuadFilter.HighPassFilter(Constants.OUTPUT_SAMPLE_RATE, 1700, 0.53f),
            BiQuadFilter.BandPassFilterConstantSkirtGain(Constants.OUTPUT_SAMPLE_RATE, 2801, 0.5f),
            BiQuadFilter.LowPassFilter(Constants.OUTPUT_SAMPLE_RATE, 5538, 0.05f)
        };
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int sampleCount)
    {
        var samplesRead = _source.Read(buffer, offset, sampleCount);
        // Get the wet/dry amount (effect strength) from settings
        float radioEffectsAmount = Math.Clamp(
            _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.RadioEffectsAmount), 0f, 2f);

        // If slider is at 0, skip all processing
        if (radioEffectsAmount <= 0.0001f || samplesRead <= 0)
            return samplesRead;

        for (var n = 0; n < sampleCount; n++)
        {
            var audio = buffer[offset + n];
            if (audio == 0) continue;
            // because we have silence in one channel (if a user picks radio left or right ear) we don't want to transform it or it'll play in both

            if (_globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioEffectsClipping))
            {
                if (audio > CLIPPING_MAX)
                    audio = CLIPPING_MAX;
                else if (audio < CLIPPING_MIN) audio = CLIPPING_MIN;
            }

            float effected = audio;
            for (var i = 0; i < _filters.Length; i++)
            {
                var filter = _filters[i];
                effected = filter.Transform(effected);

                if (double.IsNaN(effected))
                    effected = audio;
            }

            // Wet/dry mix: blend original and effected signal
            buffer[offset + n] = (audio * (1.0f - radioEffectsAmount) + effected * radioEffectsAmount) * BOOST;
        }

        //   Console.WriteLine("Read:"+samplesRead+" Time - " + _stopwatch.ElapsedMilliseconds);
        //     _stopwatch.Restart();
        //
        return samplesRead;
    }
}