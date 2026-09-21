namespace AudioMatcher.App.Services;

public interface IAudioPlaybackService : IDisposable
{
    bool IsPlaying { get; }

    bool HasAudio { get; }

    TimeSpan Position { get; set; }

    TimeSpan Duration { get; }

    event EventHandler? PlaybackStopped;

    void Open(string filePath);

    void Play();

    void Pause();

    void Stop();

    void Close();
}
