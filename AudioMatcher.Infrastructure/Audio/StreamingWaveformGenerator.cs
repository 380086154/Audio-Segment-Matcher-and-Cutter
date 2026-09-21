using AudioMatcher.Core.Interfaces;
using AudioMatcher.Core.Models;

namespace AudioMatcher.Infrastructure.Audio;

public sealed class StreamingWaveformGenerator : IWaveformGenerator
{
    private readonly IAudioDecoder _decoder;
    private readonly FFmpeg.FFmpegLocator _locator;
    private readonly FFmpeg.IFFmpegProcessRunner _runner;

    public StreamingWaveformGenerator(
        IAudioDecoder decoder,
        FFmpeg.FFmpegLocator locator,
        FFmpeg.IFFmpegProcessRunner runner)
    {
        _decoder = decoder;
        _locator = locator;
        _runner = runner;
    }

    public async Task<WaveformData> GenerateAsync(
        string filePath,
        int bucketCount,
        CancellationToken cancellationToken = default)
    {
        if (bucketCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bucketCount));
        }

        var info = await _decoder.GetInfoAsync(filePath, cancellationToken).ConfigureAwait(false);
        if (info.Duration <= TimeSpan.Zero)
        {
            return WaveformData.Empty;
        }

        const int sampleRate = 4000;
        var peaks = new float[bucketCount];
        var totalExpected = Math.Max(1, (int)Math.Round(info.Duration.TotalSeconds * sampleRate));
        var totalSamples = 0;
        var carry = Array.Empty<byte>();

        await _runner.RunStreamingAsync(
            _locator.RequireFfmpeg(),
            [
                "-hide_banner", "-nostdin", "-v", "error",
                "-i", filePath,
                "-vn",
                "-ac", "1",
                "-ar", sampleRate.ToString(),
                "-f", "f32le",
                "pipe:1"
            ],
            (data, count, _) =>
            {
                var combinedLength = carry.Length + count;
                var completeBytes = combinedLength - (combinedLength % sizeof(float));
                if (completeBytes > 0)
                {
                    var complete = new byte[completeBytes];
                    if (carry.Length > 0)
                    {
                        Buffer.BlockCopy(carry, 0, complete, 0, carry.Length);
                    }

                    Buffer.BlockCopy(data, 0, complete, carry.Length, completeBytes - carry.Length);
                    var sampleCount = completeBytes / sizeof(float);
                    var samples = new float[sampleCount];
                    Buffer.BlockCopy(complete, 0, samples, 0, completeBytes);
                    Accumulate(samples, peaks, ref totalSamples, totalExpected);
                }

                var remain = combinedLength - completeBytes;
                if (remain == 0)
                {
                    carry = [];
                }
                else
                {
                    var next = new byte[remain];
                    Buffer.BlockCopy(data, count - remain, next, 0, remain);
                    carry = next;
                }

                return ValueTask.CompletedTask;
            },
            cancellationToken).ConfigureAwait(false);

        Normalize(peaks);
        return new WaveformData(peaks, info.Duration);
    }

    private static void Accumulate(float[] samples, float[] peaks, ref int totalSamples, int totalExpected)
    {
        for (var i = 0; i < samples.Length; i++)
        {
            var index = totalSamples + i;
            var bucket = (int)((long)index * peaks.Length / totalExpected);
            if (bucket >= peaks.Length)
            {
                bucket = peaks.Length - 1;
            }

            var abs = Math.Abs(samples[i]);
            if (abs > peaks[bucket])
            {
                peaks[bucket] = abs;
            }
        }

        totalSamples += samples.Length;
    }

    private static void Normalize(float[] peaks)
    {
        var max = 0f;
        foreach (var peak in peaks)
        {
            if (peak > max)
            {
                max = peak;
            }
        }

        if (max <= 0)
        {
            return;
        }

        for (var i = 0; i < peaks.Length; i++)
        {
            peaks[i] /= max;
        }
    }
}
