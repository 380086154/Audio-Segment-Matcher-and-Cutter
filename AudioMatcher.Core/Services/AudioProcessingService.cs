using AudioMatcher.Core.Interfaces;
using AudioMatcher.Core.Models;

namespace AudioMatcher.Core.Services;

public sealed class AudioProcessingService : IAudioProcessingService
{
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
        var results = new List<ProcessingFileResult>(requests.Count);
        for (var i = 0; i < requests.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ProcessingFileResult result;
            try
            {
                result = await _processor.ProcessAsync(requests[i], cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                result = new ProcessingFileResult
                {
                    SourceFilePath = requests[i].SourceFilePath,
                    Status = ProcessingFileStatus.Cancelled,
                    Action = requests[i].Action,
                    Message = "Cancelled before the output file was written."
                };
                results.Add(result);
                _logger.Log(result);
                throw;
            }
            catch (Exception ex)
            {
                result = ProcessingFileResult.Fail(requests[i].SourceFilePath, ex.Message);
            }

            results.Add(result);
            _logger.Log(result);
            progress?.Report(new ProcessingProgress(i + 1, requests.Count, result));
        }

        return new ProcessingBatchResult { Files = results };
    }
}
