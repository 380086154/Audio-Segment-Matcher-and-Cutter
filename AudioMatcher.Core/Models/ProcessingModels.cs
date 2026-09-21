namespace AudioMatcher.Core.Models;

public enum ProcessingAction
{
    RemoveAllMatches,
    CutBeforeFirstMatch,
    CutAfterFirstMatch
}

public enum OutputConflictPolicy
{
    Skip
}

public enum ProcessingFileStatus
{
    Success,
    Failed,
    Skipped,
    Cancelled
}

public sealed class KeepSegment
{
    public KeepSegment(TimeSpan start, TimeSpan end)
    {
        if (end <= start)
        {
            throw new ArgumentOutOfRangeException(nameof(end), "Segment end must be greater than start.");
        }

        Start = start;
        End = end;
    }

    public TimeSpan Start { get; }

    public TimeSpan End { get; }

    public TimeSpan Duration => End - Start;
}

public sealed class ProcessingRequest
{
    public required string SourceFilePath { get; init; }

    public required string SearchRoot { get; init; }

    public required string OutputFolder { get; init; }

    public required ProcessingAction Action { get; init; }

    public required TimeSpan FileDuration { get; init; }

    public required IReadOnlyList<AudioMatch> Matches { get; init; }

    public OutputConflictPolicy ConflictPolicy { get; init; } = OutputConflictPolicy.Skip;
}

public sealed class ProcessingFileResult
{
    public required string SourceFilePath { get; init; }

    public string? OutputFilePath { get; init; }

    public required ProcessingFileStatus Status { get; init; }

    public ProcessingAction Action { get; init; }

    public int MatchCount { get; init; }

    public TimeSpan? FirstMatchStart { get; init; }

    public string? Message { get; init; }

    public static ProcessingFileResult Success(ProcessingRequest request, string outputPath)
        => new()
        {
            SourceFilePath = request.SourceFilePath,
            OutputFilePath = outputPath,
            Status = ProcessingFileStatus.Success,
            Action = request.Action,
            MatchCount = request.Matches.Count,
            FirstMatchStart = Matching.FirstMatchSelector.Select(request.Matches)?.Start
        };

    public static ProcessingFileResult Skip(string source, string output, string reason)
        => new()
        {
            SourceFilePath = source,
            OutputFilePath = output,
            Status = ProcessingFileStatus.Skipped,
            Message = reason
        };

    public static ProcessingFileResult Fail(string source, string error)
        => new()
        {
            SourceFilePath = source,
            Status = ProcessingFileStatus.Failed,
            Message = error
        };
}

public sealed class ProcessingProgress
{
    public ProcessingProgress(int completed, int total, ProcessingFileResult lastResult)
    {
        Completed = completed;
        Total = total;
        LastResult = lastResult;
    }

    public int Completed { get; }

    public int Total { get; }

    public ProcessingFileResult LastResult { get; }
}

public sealed class ProcessingBatchResult
{
    public IReadOnlyList<ProcessingFileResult> Files { get; init; } = [];

    public int SuccessCount => Files.Count(f => f.Status == ProcessingFileStatus.Success);

    public int FailedCount => Files.Count(f => f.Status == ProcessingFileStatus.Failed);

    public int SkippedCount => Files.Count(f => f.Status == ProcessingFileStatus.Skipped);
}
