using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;
using NAudio.Wave;
using System;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers;

public class RadioFilter : ISampleProvider
{
    public static readonly float CLIPPING_MAX = 4000 / 32768f;
    public static readonly float CLIPPING_MIN = -CLIPPING_MAX;

    private class RadioModel
    {
        public DeferredSourceProvider TxSource { get; } = new DeferredSourceProvider();

        public ISampleProvider TxEffectProvider { get; set; }

        public RadioModel(Models.Dto.RadioModel dtoPreset)
        {
            TxEffectProvider = dtoPreset.TxEffect.ToSampleProvider(TxSource);
        }
    }

    private RadioModel Arc210 { get; } = new RadioModel(DefaultRadioModels.BuildArc210());

    public RadioFilter(ISampleProvider sampleProvider)
    {
//        var samplesRead = _source.Read(buffer, offset, sampleCount);
        // Get the wet/dry amount (effect strength) from settings
//        float radioEffectsAmount = Math.Clamp(
//            _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.RadioEffectsAmount), 0f, 2f);

        // If slider is at 0, skip all processing
//        if (radioEffectsAmount <= 0.0001f || samplesRead <= 0)
//            return samplesRead;

//        for (var n = 0; n < sampleCount; n++)
//        {
//            var audio = buffer[offset + n];
//            if (audio == 0) continue;
            // because we have silence in one channel (if a user picks radio left or right ear) we don't want to transform it or it'll play in both

//            if (_globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioEffectsClipping))
//            {
//                if (audio > CLIPPING_MAX)
//                    audio = CLIPPING_MAX;
//                else if (audio < CLIPPING_MIN) audio = CLIPPING_MIN;
//            }

//            float effected = audio;
//            for (var i = 0; i < _filters.Length; i++)
//            {
//                var filter = _filters[i];
//                effected = filter.Transform(effected);

//                if (double.IsNaN(effected))
//                    effected = audio;
//            }

            // Wet/dry mix: blend original and effected signal
//            float dry = Math.Max(1.0f - radioEffectsAmount, 0.0f);
//            float wet = radioEffectsAmount;
//            buffer[offset + n] = (audio * dry + effected * wet) * BOOST;
//        }

        //   Console.WriteLine("Read:"+samplesRead+" Time - " + _stopwatch.ElapsedMilliseconds);
        //     _stopwatch.Restart();
        //
//        return samplesRead;
        Arc210.TxSource.Source = sampleProvider;
    }

    public WaveFormat WaveFormat => Arc210.TxEffectProvider.WaveFormat;

    public int Read(float[] buffer, int offset, int sampleCount)
    {
        return Arc210.TxEffectProvider.Read(buffer, offset, sampleCount);
    }
}