using AudioMatcher.Core.Interfaces;
using AudioMatcher.Core.Models;

namespace AudioMatcher.Core.Services;

public sealed class AudioProcessingService : IAudioProcessingService
{
    public const int DefaultParallelism = 10;

    private readonly IAudioProcessor _processor;
    private readonly IProcessingLogger _logger;

    public AudioProcessingService(IAudioProcessor processor, IProcessingLogger logger)
    {
        _processor = processor;
        _logger = logger;
    }

    public async Task<ProcessingBatchResult> ProcessAsync(
        IReadOnlyList<ProcessingRequest> requests,
        IProgress<ProcessingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);
        if (requests.Count == 0)
        {
            return new ProcessingBatchResult { Files = [] };
        }

        var results = new ProcessingFileResult?[requests.Count];
        var completed = 0;
        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = DefaultParallelism
        };

        try
        {
            await Parallel.ForEachAsync(Enumerable.Range(0, requests.Count), parallelOptions, async (index, token) =>
            {
                ProcessingFileResult result;
                try
                {
                    result = await _processor.ProcessAsync(requests[index], token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    result = ProcessingFileResult.Fail(requests[index].SourceFilePath, ex.Message);
                }

                results[index] = result;
                _logger.Log(result);
                var current = Interlocked.Increment(ref completed);
                progress?.Report(new ProcessingProgress(current, requests.Count, result));
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Keep files that already finished. Incomplete FFmpeg output is discarded by the processor.
        }

        return new ProcessingBatchResult { Files = results.OfType<ProcessingFileResult>().ToArray() };
    }
}
