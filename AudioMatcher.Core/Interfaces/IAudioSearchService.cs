using AudioMatcher.Core.Models;

namespace AudioMatcher.Core.Interfaces;

public interface IAudioSearchService
{
    Task<IReadOnlyList<AudioMatchResult>> SearchAsync(
        AudioSample sample,
        SearchRequest request,
        IProgress<SearchProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
