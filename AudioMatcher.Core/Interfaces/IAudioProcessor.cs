using AudioMatcher.Core.Models;

namespace AudioMatcher.Core.Interfaces;

public interface IAudioProcessor
{
    Task<ProcessingFileResult> ProcessAsync(ProcessingRequest request, CancellationToken cancellationToken = default);
}

public interface IAudioProcessingService
{
    Task<ProcessingBatchResult> ProcessAsync(
        IReadOnlyList<ProcessingRequest> requests,
        IProgress<ProcessingProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
