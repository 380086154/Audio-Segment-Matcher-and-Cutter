namespace AudioMatcher.Core.Models;

public sealed class SearchRequest
{
    public required string FolderPath { get; init; }

    public bool IncludeSubfolders { get; init; } = true;

    public MatchOptions MatchOptions { get; init; } = MatchOptions.Normal;
}

public sealed class SearchProgress
{
    public SearchProgress(int processed, int total, int matched, string? currentFile)
    {
        Processed = processed;
        Total = total;
        Matched = matched;
        CurrentFile = currentFile;
    }

    public int Processed { get; }

    public int Total { get; }

    public int Matched { get; }

    public string? CurrentFile { get; }

    public double Fraction => Total == 0 ? 0 : Processed / (double)Total;
}
