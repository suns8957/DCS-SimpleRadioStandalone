using System;

namespace Ciribob.FS3D.SimpleRadio.Standalone.ExternalAudioClient.Timers;

/// <summary>
///     Source: https://github.com/cabhishek/Time
/// </summary>
public interface ITimer
{
    void Start();
    void Stop();
    void UpdateTimeInterval(TimeSpan interval);
}