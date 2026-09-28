using AudioMatcher.Core.Models;

namespace AudioMatcher.Core.Interfaces;

public interface IAudioDecoder
{
    Task<AudioInfo> GetInfoAsync(string filePath, CancellationToken cancellationToken = default);

    Task<PcmAudio> DecodeAsync(
        string filePath,
        int sampleRate,
        int channels = 1,
        CancellationToken cancellationToken = default);

    Task<PcmAudio> DecodeSegmentAsync(
        string filePath,
        TimeSpan start,
        TimeSpan end,
        int sampleRate,
        int channels = 1,
        CancellationToken cancellationToken = default);
}
