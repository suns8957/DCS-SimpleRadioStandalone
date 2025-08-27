using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.NAudioLame;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Concentus;
using Concentus.Oggfile;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Recording
{
    internal class AudioRecordingOpusWriter : AudioRecordingWriterBase
    {

        private readonly List<OpusOggWriteStream> _opusFileWriters = new();
        public AudioRecordingOpusWriter(List<AudioRecordingStream> streams, int sampleRate, int maxSamples)
       : base(streams, sampleRate, maxSamples)
        {
        }

        public override void Start()
        {
            // streams are stored in Recordings directory, named "<date>-<time><tag>.mp3"
            var sanitisedDate = string.Join("-", DateTime.Now.ToShortDateString().Split(Path.GetInvalidFileNameChars()));
            var sanitisedTime = string.Join("-", DateTime.Now.ToLongTimeString().Split(Path.GetInvalidFileNameChars()));
            var filePathBase = $"Recordings\\{sanitisedDate}-{sanitisedTime}";

            // #TODO: interpret
            var lamePreset = (LAMEPreset)Enum.Parse(typeof(LAMEPreset),
                GlobalSettingsStore.Instance.GetClientSetting(GlobalSettingsKeys.RecordingQuality).RawValue);

            for (var i = 0; i < Streams.Count; i++)
            {
                var encoder = OpusCodecFactory.CreateEncoder(48000, 1);
                encoder.UseDTX = true; // Discontinued transmissions. Recordings are mostly silence.

                var tag = Streams[i].Tag;
                if (tag == null || tag.Length == 0) tag = "";

                var tags = new OpusTags();
                tags.Fields[OpusTagName.Artist] = "SRS " + UpdaterChecker.VERSION;
                var path = filePathBase + tag + ".opus";

                var stream = new FileStream(path, FileMode.Create);
                _opusFileWriters.Add(new OpusOggWriteStream(encoder, stream, tags, SampleRate));
            }
        }

        public override void Stop()
        {
            foreach (var stream in _opusFileWriters)
            {
                stream.Finish();
            }
        }

        protected override void DoPrepareProcessAudio()
        {
            if (_opusFileWriters.Count == 0) Start();
        }

        protected override void DoProcessAudioStream(int streamIndex, ReadOnlySpan<float> samples)
        {
            var floatPool = ArrayPool<float>.Shared;
            var floatSamples = new float[samples.Length];
            samples.CopyTo(floatSamples);
            _opusFileWriters[streamIndex].WriteSamples(floatSamples, 0, samples.Length);
        }
    }
}
