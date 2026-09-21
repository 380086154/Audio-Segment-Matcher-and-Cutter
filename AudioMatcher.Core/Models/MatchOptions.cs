namespace AudioMatcher.Core.Models;

public enum MatchTolerance
{
    Strict,
    Normal,
    Relaxed
}

public sealed record MatchOptions
{
    public MatchTolerance Tolerance { get; init; } = MatchTolerance.Normal;

    public double MinConfidence { get; init; } = 0.72;

    public int AnalysisSampleRate { get; init; } = 8000;

    public double MinPeakSeparationRatio { get; init; } = 0.5;

    public int MaxDegreeOfParallelism { get; init; } = DefaultParallelism;

    public static int DefaultParallelism => Math.Clamp(Environment.ProcessorCount / 2, 1, 4);

    public static MatchOptions Normal { get; } = new()
    {
        Tolerance = MatchTolerance.Normal,
        MinConfidence = 0.72
    };

    public static MatchOptions Strict { get; } = new()
    {
        Tolerance = MatchTolerance.Strict,
        MinConfidence = 0.85
    };

    public static MatchOptions Relaxed { get; } = new()
    {
        Tolerance = MatchTolerance.Relaxed,
        MinConfidence = 0.58
    };

    public static MatchOptions ForTolerance(MatchTolerance tolerance) => tolerance switch
    {
        MatchTolerance.Strict => Strict,
        MatchTolerance.Relaxed => Relaxed,
        _ => Normal
    };
}
