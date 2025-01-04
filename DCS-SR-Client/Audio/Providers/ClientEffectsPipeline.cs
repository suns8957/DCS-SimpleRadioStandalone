using System;
using System.Collections.Generic;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Managers;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Client.DSP;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Setting;
using MathNet.Filtering;
using MathNet.Filtering.IIR;
using NAudio.Dsp;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Providers
{
    namespace Filters
    {

        class CompositeFilter : IOnlineFilter
        {
            private List<IOnlineFilter> Filters;

            public CompositeFilter(int capacity)
            {
                Filters = new List<IOnlineFilter>(capacity);
            }

            public void Add(IOnlineFilter filter)
            {
                Filters.Add(filter);
            }

            public void AddRange(IEnumerable<IOnlineFilter> filters)
            {
                Filters.AddRange(filters);
            }
            public double ProcessSample(double sample)
            {
                foreach (var filter in Filters)
                {
                    sample = filter.ProcessSample(sample);
                }

                return sample;
            }

            public double[] ProcessSamples(double[] samples)
            {
                if (samples == null)
                {
                    return null;
                }

                double[] array = new double[samples.Length];
                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i] = ProcessSample(samples[i]);
                }

                return samples;
            }

            public void Reset()
            {
                Filters.Clear();
            }
        }
        class ClippingFilter : IOnlineFilter
        {
            private double Min { get; set; }
            private double Max { get; set; }

            public ClippingFilter(double min, double max)
            {
                Min = min;
                Max = max;
            }

            public double ProcessSample(double sample)
            {
                return Math.Max(Math.Min(sample, Max), Min);
            }

            public double[] ProcessSamples(double[] samples)
            {
                if (samples == null)
                {
                    return null;
                }

                double[] array = new double[samples.Length];
                for (int i = 0; i < samples.Length; i++)
                {
                    array[i] = ProcessSample(samples[i]);
                }

                return array;
            }

            public void Reset()
            {
            }
        }

        class NaNFilter : IOnlineFilter
        {
            private Random Rng = new Random();

            public double ProcessSample(double sample)
            {
                if (!double.IsNaN(sample))
                {
                    return sample;
                }

                // If we get a NaN, generate a bit of noise, scaled down (to avoid screeching sounds).
                return Rng.NextDouble() / 2 - 0.25; // [0, 1] / 2 - 0.25 --> [-0.25, 0.25]
            }

            public double[] ProcessSamples(double[] samples)
            {
                if (samples == null)
                {
                    return null;
                }

                double[] array = new double[samples.Length];
                for (int i = 0; i < samples.Length; i++)
                {
                    array[i] = ProcessSample(samples[i]);
                }

                return array;
            }

            public void Reset()
            {
            }
        }
        class CachedAudioEffectFilter : IOnlineFilter
        {
            public bool Enabled { get; set; } = true;
            public double Volume { get; set; } = 0;
            public bool Active
            {
                get
                {
                    return Enabled && Effect.Loaded;
                }
            }

            private int Position { get; set; } = 0;


            private CachedAudioEffect Effect;

            public CachedAudioEffectFilter(CachedAudioEffect Effect)
            {
                this.Effect = Effect;
            }

            public double ProcessSample(double sample)
            {
                var tone = Effect.AudioEffectFloat;
                sample += tone[Position] * Volume;
                Position++;

                Position = PositionRollover(Position, tone.Length);

                return sample;
            }
            public double[] ProcessSamples(double[] samples)
            {
                if (samples == null)
                {
                    return null;
                }

                double[] array = new double[samples.Length];
                for (int i = 0; i < samples.Length; i++)
                {
                    array[i] = ProcessSample(samples[i]);
                }

                return array;
            }

            protected virtual int PositionRollover(int position, int toneLength)
            {
                if (position == toneLength)
                {
                    position = 0;
                }

                return position;
            }
            public void Reset()
            {
                Enabled = false;
                Position = 0;
                Volume = 0;
            }
        }

        class HaveQuickFilter : CachedAudioEffectFilter
        {
            private readonly Random _random = new Random();
            private static readonly double HQ_RESET_CHANCE = 0.8;
            public HaveQuickFilter(CachedAudioEffect haveQuickTone)
                : base(haveQuickTone)
            {
            }

            protected override int PositionRollover(int position, int toneLength)
            {
                if (position == toneLength)
                {
                    var reset = _random.NextDouble();

                    if (reset > HQ_RESET_CHANCE)
                    {
                        position = 0;
                    }
                    else
                    {
                        //one back to try again
                        position -= 1;
                    }
                }

                return position;
            }
        }
    }
   

    public class ClientEffectsPipeline
    {
        private readonly Random _random = new Random();

        private IOnlineFilter _bandpassFilter = OnlineIirFilter.CreateBandpass(ImpulseResponse.Finite, AudioManager.OUTPUT_SAMPLE_RATE, 560, 3900);
        private Dictionary<RadioInformation.Modulation, Filters.CachedAudioEffectFilter> _toneFilters = new Dictionary<RadioInformation.Modulation, Filters.CachedAudioEffectFilter>();

        private readonly BiQuadFilter _highPassFilter;
        private readonly BiQuadFilter _lowPassFilter;

        private IOnlineFilter _clippingFilter = new Filters.ClippingFilter(RadioFilter.CLIPPING_MIN, RadioFilter.CLIPPING_MAX);
        private IOnlineFilter _nanFilter = new Filters.NaNFilter();
        private IOnlineFilter _gainFilter = new Filters.ClippingFilter(-1.0, 1.0);

        private Filters.CachedAudioEffectFilter _uhfNoise;
        private Filters.CachedAudioEffectFilter _vhfNoise;
        private Filters.CachedAudioEffectFilter _hfNoise;
        private Filters.CachedAudioEffectFilter _fmNoise;

        private readonly CachedAudioEffectProvider effectProvider = CachedAudioEffectProvider.Instance;

        private bool radioEffectsEnabled;
        private bool clippingEnabled;

        private long lastRefresh = 0; //last refresh of settings

        private readonly Settings.ProfileSettingsStore profileSettings;

        private bool radioEffects;
        private bool radioBackgroundNoiseEffect;

        private Filters.CachedAudioEffectFilter _amCollision;

        private bool irlRadioRXInterference = false;

        private readonly SyncedServerSettings serverSettings;
        
        public ClientEffectsPipeline()
        {
            profileSettings = Settings.GlobalSettingsStore.Instance.ProfileSettingsStore;
            serverSettings =  SyncedServerSettings.Instance;

            _toneFilters[RadioInformation.Modulation.FM] = new Filters.CachedAudioEffectFilter(effectProvider.NATOTone);
            _toneFilters[RadioInformation.Modulation.SINCGARS] = _toneFilters[RadioInformation.Modulation.FM];
            _toneFilters[RadioInformation.Modulation.HAVEQUICK] = new Filters.HaveQuickFilter(effectProvider.HAVEQUICKTone);

            _uhfNoise = new Filters.CachedAudioEffectFilter(effectProvider.UHFNoise);
            _vhfNoise = new Filters.CachedAudioEffectFilter(effectProvider.VHFNoise);
            _hfNoise = new Filters.CachedAudioEffectFilter(effectProvider.HFNoise);
            _fmNoise = new Filters.CachedAudioEffectFilter(effectProvider.FMNoise);

            _amCollision = new Filters.CachedAudioEffectFilter(effectProvider.AMCollision);

            _highPassFilter = BiQuadFilter.HighPassFilter(AudioManager.OUTPUT_SAMPLE_RATE, 520, 0.97f);
            _lowPassFilter = BiQuadFilter.LowPassFilter(AudioManager.OUTPUT_SAMPLE_RATE, 4130, 2.0f);
            RefreshSettings();
        }

        private void RefreshSettings()
        {
            //only get settings every 3 seconds - and cache them - issues with performance
            long now = DateTime.Now.Ticks;

            if (TimeSpan.FromTicks(now - lastRefresh).TotalSeconds > 3) //3 seconds since last refresh
            {
                lastRefresh = now;

                _toneFilters[RadioInformation.Modulation.FM].Enabled = profileSettings.GetClientSettingBool(ProfileSettingsKeys.NATOTone);
                _toneFilters[RadioInformation.Modulation.FM].Volume = profileSettings.GetClientSettingFloat(ProfileSettingsKeys.NATOToneVolume);

                _toneFilters[RadioInformation.Modulation.HAVEQUICK].Enabled = profileSettings.GetClientSettingBool(ProfileSettingsKeys.HAVEQUICKTone);
                _toneFilters[RadioInformation.Modulation.HAVEQUICK].Volume = profileSettings.GetClientSettingFloat(ProfileSettingsKeys.HQToneVolume);

                radioEffectsEnabled = profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioEffects);
                clippingEnabled = profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioEffectsClipping);
               
                
                _amCollision.Volume = profileSettings.GetClientSettingFloat(ProfileSettingsKeys.AMCollisionVolume);

                _fmNoise.Volume = profileSettings.GetClientSettingFloat(ProfileSettingsKeys.FMNoiseVolume);
                _hfNoise.Volume = profileSettings.GetClientSettingFloat(ProfileSettingsKeys.HFNoiseVolume);
                _uhfNoise.Volume = profileSettings.GetClientSettingFloat(ProfileSettingsKeys.UHFNoiseVolume);
                _vhfNoise.Volume = profileSettings.GetClientSettingFloat(ProfileSettingsKeys.VHFNoiseVolume);

                radioEffects = profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioEffects);

                radioBackgroundNoiseEffect = profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioBackgroundNoiseEffect) ;

                irlRadioRXInterference = serverSettings.GetSettingAsBool(ServerSettingsKeys.IRL_RADIO_RX_INTERFERENCE);
            }
        }

        public float[] ProcessClientTransmissions(float[] tempBuffer, List<DeJitteredTransmission> transmissions, out int clientTransmissionLength)
        {
            RefreshSettings();
            DeJitteredTransmission lastTransmission = transmissions[0];

            clientTransmissionLength = 0;
            foreach (var transmission in transmissions)
            {
                for (int i = 0; i < transmission.PCMAudioLength; i++)
                {
                    tempBuffer[i] += transmission.PCMMonoAudio[i];
                }

                clientTransmissionLength = Math.Max(clientTransmissionLength, transmission.PCMAudioLength);
            }

            bool process = true;

            // take info account server setting AND volume of this radio AND if its AM or FM
            // FOR HAVEQUICK - only if its MORE THAN TWO
            if (lastTransmission.ReceivedRadio != 0
                && !lastTransmission.NoAudioEffects
                && (lastTransmission.Modulation == RadioInformation.Modulation.AM
                    || lastTransmission.Modulation == RadioInformation.Modulation.FM
                    || lastTransmission.Modulation == RadioInformation.Modulation.SINCGARS
                    || lastTransmission.Modulation == RadioInformation.Modulation.HAVEQUICK)
                && irlRadioRXInterference)
            {
                if (transmissions.Count > 1)
                {
                    //All AM is wrecked if more than one transmission
                    //For HQ - only if more than TWO transmissions and its totally fucked
                    if (lastTransmission.Modulation == RadioInformation.Modulation.HAVEQUICK && transmissions.Count > 2 && _amCollision.Active)
                    {
                        //replace the buffer with our own
                        int outIndex = 0;
                        while (outIndex < clientTransmissionLength)
                        {
                            tempBuffer[outIndex++] = (float)(_amCollision.ProcessSample(0) * lastTransmission.Volume);
                        }

                        process = false;
                    }
                    else if (lastTransmission.Modulation == RadioInformation.Modulation.AM && _amCollision.Active)
                    {
                        //AM https://www.youtube.com/watch?v=yHRDjhkrDbo
                        //Heterodyne tone AND audio from multiple transmitters in a horrible mess
                        //TODO improve this

              
                        //process here first
                        tempBuffer = ProcessClientAudioSamples(tempBuffer, clientTransmissionLength, 0, lastTransmission);
                        process = false;

                        //apply heterodyne tone to the mixdown
                        //replace the buffer with our own
                        var initialVolume = _amCollision.Volume;
                        _amCollision.Volume *= lastTransmission.Volume;
                        for (int outIndex = 0; outIndex < clientTransmissionLength; ++outIndex)
                        {
                            tempBuffer[outIndex] = (float)_amCollision.ProcessSample(tempBuffer[outIndex]);
                        }
                        _amCollision.Volume = initialVolume;
                    }
                    else if (lastTransmission.Modulation == RadioInformation.Modulation.FM || lastTransmission.Modulation == RadioInformation.Modulation.SINCGARS)
                    {
                        //FM picketing / picket fencing - pick one transmission at random
                        //TODO improve this to pick the stronger frequency?

                        int index = _random.Next(transmissions.Count);
                        var transmission = transmissions[index];

                        Array.Copy(transmission.PCMMonoAudio, tempBuffer, transmission.PCMMonoAudio.Length);
                        clientTransmissionLength = transmission.PCMMonoAudio.Length;
                    }
                }
            }

            //only process if AM effect doesnt apply
            if (process)
                tempBuffer = ProcessClientAudioSamples(tempBuffer, clientTransmissionLength, 0, lastTransmission);


            return tempBuffer;
        }

        public float[] ProcessClientAudioSamples(float[] buffer, int count, int offset, DeJitteredTransmission transmission)
        {
            if (!transmission.NoAudioEffects)
            {
                if (transmission.Modulation == RadioInformation.Modulation.MIDS
                    || transmission.Modulation == RadioInformation.Modulation.SATCOM
                    || transmission.Modulation == RadioInformation.Modulation.INTERCOM)
                {
                    if (radioEffects)
                    {
                        AddRadioEffectIntercom(buffer, count, offset, transmission.Modulation);
                    }
                }
                else
                {
                    AddRadioEffect(buffer, count, offset, transmission.Modulation, transmission.Frequency);
                }
            }

            //final adjust
            AdjustVolume(buffer, count, offset, transmission.Volume);

            return buffer;
        }

        private void AdjustVolume(float[] buffer, int count, int offset, float volume)
        {
            int outputIndex = offset;
            while (outputIndex < offset + count)
            {
                buffer[outputIndex] *= volume;

                outputIndex++;
            }
        }

        private void AddRadioEffectIntercom(float[] buffer, int count, int offset,RadioInformation.Modulation modulation)
        {
            int outputIndex = offset;
            while (outputIndex < offset + count)
            {
                var audio = _highPassFilter.Transform(buffer[outputIndex]);

                audio = _highPassFilter.Transform(audio);

                if (float.IsNaN(audio))
                    audio = _lowPassFilter.Transform(buffer[outputIndex]);
                else
                    audio = _lowPassFilter.Transform(audio);

                if (!float.IsNaN(audio))
                {
                    // clip
                    if (audio > 1.0f)
                        audio = 1.0f;
                    if (audio < -1.0f)
                        audio = -1.0f;

                    buffer[outputIndex] = audio;
                }

                outputIndex++;
            }
        }

        private Filters.CachedAudioEffectFilter GetNoiseEffect(RadioInformation.Modulation modulation, double freq)
        {
            switch (modulation)
            {
                case RadioInformation.Modulation.AM:
                case RadioInformation.Modulation.HAVEQUICK:
                    if (freq > 200e6) // UHF range
                    {
                        return _uhfNoise;
                    }
                    
                    if (freq > 80e6)
                    {
                        return _vhfNoise;
                    }

                    return _hfNoise;
                case RadioInformation.Modulation.FM:
                case RadioInformation.Modulation.SINCGARS:
                    return _fmNoise;
            }

            return null;
        }

        private void AddRadioEffect(float[] buffer, int count, int offset, RadioInformation.Modulation modulation, double freq)
        {
            // Precompute the list of filters, so we can blaze through.
            Filters.CompositeFilter compositeFilter = new Filters.CompositeFilter(6);
            if (radioEffectsEnabled)
            {
                if (clippingEnabled)
                {
                    compositeFilter.Add(_clippingFilter);
                }

                compositeFilter.Add(_bandpassFilter);
            }

            if (_toneFilters.TryGetValue(modulation, out var toneFilter))
            {
                if (toneFilter.Active)
                {
                    compositeFilter.Add(toneFilter);
                }
            }

            if (radioBackgroundNoiseEffect)
            {
                var noise = GetNoiseEffect(modulation, freq);
                if (noise != null && noise.Active)
                {
                    compositeFilter.Add(noise);
                }
            }

            compositeFilter.Add(_nanFilter);

            compositeFilter.Add(_gainFilter);

            for (var i = 0; i < count; ++i)
            {
                buffer[offset + i] = (float)compositeFilter.ProcessSample(buffer[offset + i]);
            }
        }
    }
}
