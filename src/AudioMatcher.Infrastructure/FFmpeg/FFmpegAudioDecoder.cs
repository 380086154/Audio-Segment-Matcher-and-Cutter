using System.Globalization;
using System.Text.Json;
using AudioMatcher.Core.Interfaces;
using AudioMatcher.Core.Models;

namespace AudioMatcher.Infrastructure.FFmpeg;

public sealed class FFmpegAudioDecoder : IAudioDecoder
{
    private readonly FFmpegLocator _locator;
    private readonly IFFmpegProcessRunner _runner;

    public FFmpegAudioDecoder(FFmpegLocator locator, IFFmpegProcessRunner runner)
    {
        _locator = locator;
        _runner = runner;
    }

    public async Task<AudioInfo> GetInfoAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_locator.FfprobePath))
        {
            return await GetInfoWithFfprobeAsync(filePath, cancellationToken).ConfigureAwait(false);
        }

        return await GetInfoWithFfmpegAsync(filePath, cancellationToken).ConfigureAwait(false);
    }

    private async Task<AudioInfo> GetInfoWithFfprobeAsync(string filePath, CancellationToken cancellationToken)
    {
        var json = await _runner.RunToTextAsync(
            _locator.RequireFfprobe(),
            [
                "-v", "error",
                "-print_format", "json",
                "-show_format",
                "-show_streams",
                filePath
            ],
            cancellationToken).ConfigureAwait(false);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        TimeSpan duration = TimeSpan.Zero;
        var sampleRate = 0;
        var channels = 0;
        string? codec = null;

        if (root.TryGetProperty("format", out var format) &&
            format.TryGetProperty("duration", out var durationElement) &&
            double.TryParse(durationElement.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            duration = TimeSpan.FromSeconds(seconds);
        }

        if (root.TryGetProperty("streams", out var streams))
        {
            foreach (var stream in streams.EnumerateArray())
            {
                if (!stream.TryGetProperty("codec_type", out var type) || type.GetString() != "audio")
                {
                    continue;
                }

                codec = stream.TryGetProperty("codec_name", out var codecName) ? codecName.GetString() : null;
                if (stream.TryGetProperty("sample_rate", out var rateElement))
                {
                    int.TryParse(rateElement.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out sampleRate);
                }

                if (stream.TryGetProperty("channels", out var channelElement))
                {
                    channels = channelElement.GetInt32();
                }

                if (duration == TimeSpan.Zero &&
                    stream.TryGetProperty("duration", out var streamDuration) &&
                    double.TryParse(streamDuration.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var streamSeconds))
                {
                    duration = TimeSpan.FromSeconds(streamSeconds);
                }

                break;
            }
        }

        return new AudioInfo(filePath, duration, sampleRate, channels, codec);
    }

    private async Task<AudioInfo> GetInfoWithFfmpegAsync(string filePath, CancellationToken cancellationToken)
    {
        var stderr = await _runner.CaptureStdErrAsync(
            _locator.RequireFfmpeg(),
            ["-hide_banner", "-i", filePath],
            throwOnError: false,
            cancellationToken).ConfigureAwait(false);

        var duration = TimeSpan.Zero;
        var durationMatch = System.Text.RegularExpressions.Regex.Match(stderr, @"Duration:\s*(\d+):(\d+):(\d+\.\d+)");
        if (durationMatch.Success)
        {
            var hours = int.Parse(durationMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            var minutes = int.Parse(durationMatch.Groups[2].Value, CultureInfo.InvariantCulture);
            var seconds = double.Parse(durationMatch.Groups[3].Value, CultureInfo.InvariantCulture);
            duration = TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
        }

        var sampleRate = 0;
        var rateMatch = System.Text.RegularExpressions.Regex.Match(stderr, @"(\d+)\s*Hz");
        if (rateMatch.Success)
        {
            sampleRate = int.Parse(rateMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        var channels = stderr.Contains("stereo", StringComparison.OrdinalIgnoreCase) ? 2
            : stderr.Contains("mono", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        var codecMatch = System.Text.RegularExpressions.Regex.Match(stderr, @"Audio:\s*([a-zA-Z0-9_]+)");
        var codec = codecMatch.Success ? codecMatch.Groups[1].Value : null;
        return new AudioInfo(filePath, duration, sampleRate, channels, codec);
    }

    public Task<PcmAudio> DecodeAsync(
        string filePath,
        int sampleRate,
        int channels = 1,
        CancellationToken cancellationToken = default)
        => DecodeCoreAsync(
            [
                "-hide_banner", "-nostdin", "-v", "error",
                "-i", filePath,
                "-vn",
                "-ac", channels.ToString(CultureInfo.InvariantCulture),
                "-ar", sampleRate.ToString(CultureInfo.InvariantCulture),
                "-f", "f32le",
                "pipe:1"
            ],
            sampleRate,
            channels,
            cancellationToken);

    public Task<PcmAudio> DecodeSegmentAsync(
        string filePath,
        TimeSpan start,
        TimeSpan end,
        int sampleRate,
        int channels = 1,
        CancellationToken cancellationToken = default)
        => DecodeCoreAsync(
            [
                "-hide_banner", "-nostdin", "-v", "error",
                "-i", filePath,
                "-ss", FormatTimestamp(start),
                "-to", FormatTimestamp(end),
                "-vn",
                "-ac", channels.ToString(CultureInfo.InvariantCulture),
                "-ar", sampleRate.ToString(CultureInfo.InvariantCulture),
                "-f", "f32le",
                "pipe:1"
            ],
            sampleRate,
            channels,
            cancellationToken);

    private async Task<PcmAudio> DecodeCoreAsync(
        IReadOnlyList<string> arguments,
        int sampleRate,
        int channels,
        CancellationToken cancellationToken)
    {
        var bytes = await _runner.RunToBytesAsync(_locator.RequireFfmpeg(), arguments, cancellationToken).ConfigureAwait(false);
        var sampleCount = bytes.Length / sizeof(float);
        if (sampleCount <= 0)
        {
            return new PcmAudio([], sampleRate, channels);
        }

        var samples = new float[sampleCount];
        Buffer.BlockCopy(bytes, 0, samples, 0, sampleCount * sizeof(float));
        return new PcmAudio(samples, sampleRate, channels);
    }

    private static string FormatTimestamp(TimeSpan time) => TimeText.Format(time);
}
