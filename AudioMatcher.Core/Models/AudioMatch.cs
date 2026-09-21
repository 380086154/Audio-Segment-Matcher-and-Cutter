namespace AudioMatcher.Core.Models;

public sealed class AudioMatch
{
    public AudioMatch(string filePath, TimeSpan start, TimeSpan end, double confidence)
    {
        FilePath = filePath;
        Start = start;
        End = end;
        Confidence = confidence;
    }

    public string FilePath { get; }

    public TimeSpan Start { get; }

    public TimeSpan End { get; }

    public double Confidence { get; }

    public TimeSpan Duration => End - Start;
}

public enum MatchStatus
{
    Matched,
    NoMatch,
    Failed
}

public sealed class AudioMatchResult
{
    public AudioMatchResult(
        string filePath,
        TimeSpan fileDuration,
        IReadOnlyList<AudioMatch> matches,
        MatchStatus status = MatchStatus.NoMatch,
        string? errorMessage = null)
    {
        FilePath = filePath;
        FileDuration = fileDuration;
        Matches = matches;
        Status = status;
        ErrorMessage = errorMessage;
    }

    public string FilePath { get; }

    public string FileName => Path.GetFileName(FilePath);

    public TimeSpan FileDuration { get; }

    public IReadOnlyList<AudioMatch> Matches { get; }

    public MatchStatus Status { get; }

    public string? ErrorMessage { get; }

    public bool HasMatch => Status == MatchStatus.Matched && Matches.Count > 0;

    public AudioMatch? FirstMatch => Matching.FirstMatchSelector.Select(Matches);

    public static AudioMatchResult Matched(string filePath, TimeSpan duration, IReadOnlyList<AudioMatch> matches)
        => new(filePath, duration, matches, MatchStatus.Matched);

    public static AudioMatchResult None(string filePath, TimeSpan duration)
        => new(filePath, duration, [], MatchStatus.NoMatch);

    public static AudioMatchResult Failed(string filePath, string error)
        => new(filePath, TimeSpan.Zero, [], MatchStatus.Failed, error);
}
