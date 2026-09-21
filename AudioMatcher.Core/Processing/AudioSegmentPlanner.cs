using AudioMatcher.Core.Matching;
using AudioMatcher.Core.Models;

namespace AudioMatcher.Core.Processing;

public sealed class AudioSegmentPlanner
{
    public IReadOnlyList<KeepSegment> Plan(
        TimeSpan fileDuration,
        IReadOnlyList<AudioMatch> matches,
        ProcessingAction action)
    {
        if (fileDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(fileDuration));
        }

        return action switch
        {
            ProcessingAction.RemoveAllMatches => PlanRemoveAll(fileDuration, matches),
            ProcessingAction.CutBeforeFirstMatch => PlanCutBefore(fileDuration, matches),
            ProcessingAction.CutAfterFirstMatch => PlanCutAfter(fileDuration, matches),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
    }

    private static IReadOnlyList<KeepSegment> PlanRemoveAll(TimeSpan duration, IReadOnlyList<AudioMatch> matches)
    {
        var merged = Merge(matches);
        var keep = new List<KeepSegment>();
        var cursor = TimeSpan.Zero;
        foreach (var match in merged)
        {
            var start = Clamp(match.Start, duration);
            var end = Clamp(match.End, duration);
            if (start > cursor)
            {
                keep.Add(new KeepSegment(cursor, start));
            }

            if (end > cursor)
            {
                cursor = end;
            }
        }

        if (cursor < duration)
        {
            keep.Add(new KeepSegment(cursor, duration));
        }

        return keep;
    }

    private static IReadOnlyList<KeepSegment> PlanCutBefore(TimeSpan duration, IReadOnlyList<AudioMatch> matches)
    {
        var first = FirstMatchSelector.Select(Valid(matches));
        if (first is null)
        {
            return [];
        }

        var start = Clamp(first.Start, duration);
        if (start >= duration)
        {
            return [];
        }

        return [new KeepSegment(start, duration)];
    }

    private static IReadOnlyList<KeepSegment> PlanCutAfter(TimeSpan duration, IReadOnlyList<AudioMatch> matches)
    {
        var first = FirstMatchSelector.Select(Valid(matches));
        if (first is null)
        {
            return [];
        }

        var end = Clamp(first.End, duration);
        if (end <= TimeSpan.Zero)
        {
            return [];
        }

        return [new KeepSegment(TimeSpan.Zero, end)];
    }

    private static IEnumerable<AudioMatch> Valid(IEnumerable<AudioMatch> matches)
        => matches.Where(match => match.End > match.Start);

    private static List<AudioMatch> Merge(IEnumerable<AudioMatch> matches)
    {
        var ordered = Valid(matches).OrderBy(match => match.Start).ThenBy(match => match.End).ToList();
        var merged = new List<AudioMatch>();
        foreach (var match in ordered)
        {
            if (merged.Count == 0 || match.Start > merged[^1].End)
            {
                merged.Add(match);
                continue;
            }

            var previous = merged[^1];
            var end = match.End > previous.End ? match.End : previous.End;
            merged[^1] = new AudioMatch(previous.FilePath, previous.Start, end, Math.Max(previous.Confidence, match.Confidence));
        }

        return merged;
    }

    private static TimeSpan Clamp(TimeSpan value, TimeSpan duration)
    {
        if (value < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return value > duration ? duration : value;
    }
}
