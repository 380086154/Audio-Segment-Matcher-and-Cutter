using AudioMatcher.Core.Models;

namespace AudioMatcher.Tests.Support;

internal static class TestAudio
{
    public static PcmAudio Noise(TimeSpan duration, int sampleRate, int seed, double amplitude = 1)
    {
        var count = Math.Max(1, (int)Math.Round(duration.TotalSeconds * sampleRate));
        var rng = new Random(seed);
        var samples = new float[count];
        for (var i = 0; i < count; i++)
        {
            samples[i] = (float)((rng.NextDouble() * 2 - 1) * amplitude);
        }

        return new PcmAudio(samples, sampleRate, 1);
    }

    public static PcmAudio Embed(PcmAudio host, PcmAudio snippet, TimeSpan at, double gain = 1)
    {
        var output = (float[])host.Samples.Clone();
        var start = (int)Math.Round(at.TotalSeconds * host.SampleRate);
        for (var i = 0; i < snippet.Samples.Length && start + i < output.Length; i++)
        {
            output[start + i] = (float)(snippet.Samples[i] * gain);
        }

        return new PcmAudio(output, host.SampleRate, host.Channels);
    }

    public static PcmAudio Slice(PcmAudio audio, TimeSpan start, TimeSpan end)
    {
        var startIndex = (int)Math.Round(start.TotalSeconds * audio.SampleRate);
        var endIndex = (int)Math.Round(end.TotalSeconds * audio.SampleRate);
        startIndex = Math.Clamp(startIndex, 0, audio.Samples.Length);
        endIndex = Math.Clamp(endIndex, startIndex, audio.Samples.Length);
        var length = endIndex - startIndex;
        var samples = new float[length];
        Array.Copy(audio.Samples, startIndex, samples, 0, length);
        return new PcmAudio(samples, audio.SampleRate, audio.Channels);
    }

    public static AudioMatch Match(string path, TimeSpan start, TimeSpan end, double confidence = 0.9)
        => new(path, start, end, confidence);
}
