using AudioMatcher.Core.Models;

namespace AudioMatcher.Core.Matching;

public static class AudioMonoResampler
{
    public static float[] ToMonoResampled(PcmAudio audio, int targetSampleRate)
    {
        ArgumentNullException.ThrowIfNull(audio);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetSampleRate);

        var mono = ToMono(audio);
        if (audio.SampleRate == targetSampleRate)
        {
            return mono;
        }

        return ResampleLinear(mono, audio.SampleRate, targetSampleRate);
    }

    public static float[] ToMono(PcmAudio audio)
    {
        if (audio.Channels == 1)
        {
            return audio.Samples;
        }

        var frames = audio.FrameCount;
        var mono = new float[frames];
        var channels = audio.Channels;
        var samples = audio.Samples;
        for (var i = 0; i < frames; i++)
        {
            double sum = 0;
            var offset = i * channels;
            for (var c = 0; c < channels; c++)
            {
                sum += samples[offset + c];
            }

            mono[i] = (float)(sum / channels);
        }

        return mono;
    }

    public static float[] ResampleLinear(float[] input, int fromRate, int toRate)
    {
        if (input.Length == 0)
        {
            return [];
        }

        if (fromRate == toRate)
        {
            return input;
        }

        var ratio = fromRate / (double)toRate;
        var outputLength = Math.Max(1, (int)Math.Round(input.Length / ratio));
        var output = new float[outputLength];
        var last = input.Length - 1;
        for (var i = 0; i < outputLength; i++)
        {
            var src = i * ratio;
            var i0 = (int)src;
            if (i0 >= last)
            {
                output[i] = input[last];
                continue;
            }

            var t = (float)(src - i0);
            output[i] = input[i0] * (1 - t) + input[i0 + 1] * t;
        }

        return output;
    }
}
