namespace AudioMatcher.Core.Models;

public sealed class AudioInfo
{
    public AudioInfo(string filePath, TimeSpan duration, int sampleRate, int channels, string? codec = null)
    {
        FilePath = filePath;
        Duration = duration;
        SampleRate = sampleRate;
        Channels = channels;
        Codec = codec;
    }

    public string FilePath { get; }

    public TimeSpan Duration { get; }

    public int SampleRate { get; }

    public int Channels { get; }

    public string? Codec { get; }
}
