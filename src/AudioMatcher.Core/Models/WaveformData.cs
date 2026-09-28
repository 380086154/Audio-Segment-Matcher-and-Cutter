namespace AudioMatcher.Core.Models;

public sealed class WaveformData
{
    public WaveformData(float[] peaks, TimeSpan duration)
    {
        Peaks = peaks;
        Duration = duration;
    }

    public float[] Peaks { get; }

    public TimeSpan Duration { get; }

    public static WaveformData Empty { get; } = new([], TimeSpan.Zero);
}
