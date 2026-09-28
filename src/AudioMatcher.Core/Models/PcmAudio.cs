namespace AudioMatcher.Core.Models;

public sealed class PcmAudio
{
    public PcmAudio(float[] samples, int sampleRate, int channels)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        }

        if (channels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(channels));
        }

        Samples = samples;
        SampleRate = sampleRate;
        Channels = channels;
    }

    public float[] Samples { get; }

    public int SampleRate { get; }

    public int Channels { get; }

    public int FrameCount => Channels == 0 ? 0 : Samples.Length / Channels;

    public TimeSpan Duration => SampleRate == 0 || Channels == 0
        ? TimeSpan.Zero
        : TimeSpan.FromSeconds(Samples.Length / (double)(SampleRate * Channels));
}
