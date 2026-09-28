using NAudio.Wave;

namespace AudioMatcher.App.Services;

public sealed class NAudioPlaybackService : IAudioPlaybackService
{
    private WaveOut? _output;
    private MediaFoundationReader? _reader;
    private bool _isPlaying;

    public bool IsPlaying => _isPlaying;

    public bool HasAudio => _reader is not null;

    public TimeSpan Duration => _reader?.TotalTime ?? TimeSpan.Zero;

    public TimeSpan Position
    {
        get => _reader?.CurrentTime ?? TimeSpan.Zero;
        set
        {
            if (_reader is null)
            {
                return;
            }

            if (value < TimeSpan.Zero)
            {
                value = TimeSpan.Zero;
            }

            if (value > _reader.TotalTime)
            {
                value = _reader.TotalTime;
            }

            _reader.CurrentTime = value;
        }
    }

    public event EventHandler? PlaybackStopped;

    public void Open(string filePath)
    {
        Close();
        _reader = new MediaFoundationReader(filePath);
        _output = new WaveOut();
        _output.PlaybackStopped += OnPlaybackStopped;
        _output.Init(_reader);
    }

    public void Play()
    {
        _output?.Play();
        _isPlaying = true;
    }

    public void Pause()
    {
        _output?.Pause();
        _isPlaying = false;
    }

    public void Stop()
    {
        _output?.Stop();
        if (_reader is not null)
        {
            _reader.CurrentTime = TimeSpan.Zero;
        }

        _isPlaying = false;
    }

    public void Close()
    {
        _isPlaying = false;
        if (_output is not null)
        {
            _output.PlaybackStopped -= OnPlaybackStopped;
            _output.Stop();
            _output.Dispose();
            _output = null;
        }

        _reader?.Dispose();
        _reader = null;
    }

    public void Dispose() => Close();

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        _isPlaying = false;
        PlaybackStopped?.Invoke(this, EventArgs.Empty);
    }
}
