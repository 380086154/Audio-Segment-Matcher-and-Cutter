using AudioMatcher.Core.Models;

namespace AudioMatcher.Core.Interfaces;

public interface IWaveformGenerator
{
    Task<WaveformData> GenerateAsync(
        string filePath,
        int bucketCount,
        CancellationToken cancellationToken = default);
}
