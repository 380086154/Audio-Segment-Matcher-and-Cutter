using AudioMatcher.Core.Models;
using AudioMatcher.Core.Processing;
using AudioMatcher.Tests.Support;

namespace AudioMatcher.Tests.Processing;

public sealed class AudioSegmentPlannerTests
{
    private readonly AudioSegmentPlanner _planner = new();
    private static readonly TimeSpan Duration = TimeSpan.FromMinutes(10);
    private const string File = "chapter.mp3";

    [Fact]
    public void Remove_OneMatchInMiddle()
    {
        var keep = _planner.Plan(Duration, [TestAudio.Match(File, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(3))], ProcessingAction.RemoveAllMatches);
        Assert.Equal(2, keep.Count);
        Assert.Equal(TimeSpan.Zero, keep[0].Start);
        Assert.Equal(TimeSpan.FromMinutes(2), keep[0].End);
        Assert.Equal(TimeSpan.FromMinutes(3), keep[1].Start);
        Assert.Equal(Duration, keep[1].End);
    }

    [Fact]
    public void Remove_MultipleMatches()
    {
        var keep = _planner.Plan(Duration,
        [
            TestAudio.Match(File, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2)),
            TestAudio.Match(File, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(6))
        ], ProcessingAction.RemoveAllMatches);

        Assert.Equal(3, keep.Count);
        Assert.Equal(TimeSpan.Zero, keep[0].Start);
        Assert.Equal(TimeSpan.FromMinutes(1), keep[0].End);
        Assert.Equal(TimeSpan.FromMinutes(2), keep[1].Start);
        Assert.Equal(TimeSpan.FromMinutes(5), keep[1].End);
        Assert.Equal(TimeSpan.FromMinutes(6), keep[2].Start);
        Assert.Equal(Duration, keep[2].End);
    }

    [Fact]
    public void Remove_MatchAtBeginning()
    {
        var keep = _planner.Plan(Duration, [TestAudio.Match(File, TimeSpan.Zero, TimeSpan.FromMinutes(1))], ProcessingAction.RemoveAllMatches);
        var segment = Assert.Single(keep);
        Assert.Equal(TimeSpan.FromMinutes(1), segment.Start);
        Assert.Equal(Duration, segment.End);
    }

    [Fact]
    public void Remove_MatchAtEnd()
    {
        var keep = _planner.Plan(Duration, [TestAudio.Match(File, TimeSpan.FromMinutes(9), Duration)], ProcessingAction.RemoveAllMatches);
        var segment = Assert.Single(keep);
        Assert.Equal(TimeSpan.Zero, segment.Start);
        Assert.Equal(TimeSpan.FromMinutes(9), segment.End);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(9.5)]
    public void CutBefore_KeepsFromFirstMatchToEnd(double startMinutes)
    {
        var start = TimeSpan.FromMinutes(startMinutes);
        var keep = _planner.Plan(Duration,
        [
            TestAudio.Match(File, TimeSpan.FromMinutes(9.8), TimeSpan.FromMinutes(10)),
            TestAudio.Match(File, start, start + TimeSpan.FromSeconds(12))
        ], ProcessingAction.CutBeforeFirstMatch);

        var segment = Assert.Single(keep);
        Assert.Equal(start, segment.Start);
        Assert.Equal(Duration, segment.End);
    }

    [Theory]
    [InlineData(0, 0.2)]
    [InlineData(4, 4.2)]
    [InlineData(9, 9.2)]
    public void CutAfter_KeepsFromStartToFirstMatchEnd(double startMinutes, double endMinutes)
    {
        var keep = _planner.Plan(Duration,
        [
            TestAudio.Match(File, TimeSpan.FromMinutes(9.8), TimeSpan.FromMinutes(9.95)),
            TestAudio.Match(File, TimeSpan.FromMinutes(startMinutes), TimeSpan.FromMinutes(endMinutes))
        ], ProcessingAction.CutAfterFirstMatch);

        var segment = Assert.Single(keep);
        Assert.Equal(TimeSpan.Zero, segment.Start);
        Assert.Equal(TimeSpan.FromMinutes(endMinutes), segment.End);
    }

    [Fact]
    public void CutActions_IgnoreLaterMatches()
    {
        var matches = new[]
        {
            TestAudio.Match(File, TimeSpan.FromMinutes(7), TimeSpan.FromMinutes(7.2)),
            TestAudio.Match(File, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(2.2))
        };

        var before = Assert.Single(_planner.Plan(Duration, matches, ProcessingAction.CutBeforeFirstMatch));
        Assert.Equal(TimeSpan.FromMinutes(2), before.Start);

        var after = Assert.Single(_planner.Plan(Duration, matches, ProcessingAction.CutAfterFirstMatch));
        Assert.Equal(TimeSpan.FromMinutes(2.2), after.End);
    }
}
