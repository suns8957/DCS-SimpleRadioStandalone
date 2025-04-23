// using System.Collections.Concurrent;
// using System.Collections.Generic;
// using System.Diagnostics;
// using System.IO;
// using System.Linq;
// using System.Threading;
// using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Client;
// using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
// using NAudio.Wave;
// using NLog;
//
// namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Recording;
//
// public class AudioRecordingManager
// {
//     private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
//
//     //per frequency per client list
//     private readonly ConcurrentDictionary<double, AudioRecordingFrequencyGroup> _clientAudioWriters = new();
//     private readonly int _maxSamples;
//
//     // TODO: drop in favor of AudioManager.OUTPUT_SAMPLE_RATE
//     private readonly int _sampleRate;
//     private readonly string _sessionId;
//
//     private ConnectedClientsSingleton _connectedClientsSingleton = ConnectedClientsSingleton.Instance;
//     private bool _processThreadDone;
//
//     private bool _stop;
//
//     public AudioRecordingManager(string sessionId)
//     {
//         _sessionId = sessionId;
//         
//         _sampleRate = Constants.OUTPUT_SAMPLE_RATE;
//
//         _stop = true;
//     }
//
//     private void ProcessQueues()
//     {
//         _processThreadDone = false;
//
//         _logger.Info("Transmission recording started.");
//
//         InitFolders();
//
//         var timer = new Stopwatch();
//         timer.Start();
//
//         while (!_stop)
//         {
//             Thread.Sleep(100);
//             ProcessClientWriters(timer.ElapsedMilliseconds);
//             timer.Restart();
//         }
//
//         timer.Stop();
//
//         _logger.Info("Transmission recording ended, draining audio.");
//
//         foreach (var clientWriter in _clientAudioWriters) clientWriter.Value.Stop();
//
//         Thread.Sleep(500);
//
//         _logger.Info("Stop recording thread");
//
//         _processThreadDone = true;
//     }
//
//     private void ProcessClientWriters(long milliseconds)
//     {
//         foreach (var clientAudioPair in _clientAudioWriters) clientAudioPair.Value.ProcessClientAudio(milliseconds);
//     }
//
//     private void InitFolders()
//     {
//         if (!Directory.Exists("Recordings"))
//         {
//             _logger.Info("Recordings directory missing, creating directory");
//             Directory.CreateDirectory("Recordings");
//         }
//     }
//
//     public void Start(List<double> recordingFrequencies)
//     {
//         if (recordingFrequencies.Count == 0)
//         {
//             _processThreadDone = true;
//             _logger.Info("Transmission recording disabled");
//             return;
//         }
//
//         _logger.Info("Transmission recording waiting for audio. Frequencies Recorded: " +
//                      string.Join(",", recordingFrequencies.Select(n => n.ToString()).ToArray()));
//
//         _stop = false;
//         new Thread(ProcessQueues).Start();
//     }
//
//     ~AudioRecordingManager()
//     {
//         Stop();
//     }
//
//     public void Stop()
//     {
//         if (!_stop)
//         {
//             _stop = true;
//             for (var i = 0; !_processThreadDone && i < 10; i++) Thread.Sleep(200);
//
//             foreach (var clientWriter in _clientAudioWriters) clientWriter.Value.Stop();
//
//             _logger.Info("Transmission recording stopped.");
//         }
//     }
//
//
//     public void AddClientAudio(ClientAudio audio)
//     {
//         // find correct writer - add to the list
//         if (!_clientAudioWriters.TryGetValue(audio.Frequency, out var audioRecordingFrequencyGroup))
//         {
//             audioRecordingFrequencyGroup = new AudioRecordingFrequencyGroup(audio.Frequency,
//                 _sessionId, WaveFormat.CreateIeeeFloatWaveFormat(Constants.OUTPUT_SAMPLE_RATE, 1));
//             _clientAudioWriters[audio.Frequency] = audioRecordingFrequencyGroup;
//         }
//
//         audioRecordingFrequencyGroup.AddClientAudio(audio);
//     }
//
//     public void RemoveClientBuffer(string clientGuid)
//     {
//         foreach (var recordingGroup in _clientAudioWriters) recordingGroup.Value.RemoveClient(clientGuid);
//     }
// }

