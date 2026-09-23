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
    [InlineData(0.2)]
    [InlineData(4.2)]
    [InlineData(9.2)]
    public void CutBefore_KeepsFromFirstMatchEndToFileEnd(double endMinutes)
    {
        var keep = _planner.Plan(Duration,
        [
            TestAudio.Match(File, TimeSpan.FromMinutes(9.8), TimeSpan.FromMinutes(10)),
            TestAudio.Match(File, TimeSpan.FromMinutes(endMinutes) - TimeSpan.FromSeconds(12), TimeSpan.FromMinutes(endMinutes))
        ], ProcessingAction.CutBeforeFirstMatch);

        var segment = Assert.Single(keep);
        Assert.Equal(TimeSpan.FromMinutes(endMinutes), segment.Start);
        Assert.Equal(Duration, segment.End);
    }

    [Theory]
    [InlineData(0.2)]
    [InlineData(4)]
    [InlineData(9)]
    public void CutAfter_KeepsFromBeginningToFirstMatchStart(double startMinutes)
    {
        var start = TimeSpan.FromMinutes(startMinutes);
        var keep = _planner.Plan(Duration,
        [
            TestAudio.Match(File, TimeSpan.FromMinutes(9.8), TimeSpan.FromMinutes(9.95)),
            TestAudio.Match(File, start, start + TimeSpan.FromSeconds(12))
        ], ProcessingAction.CutAfterFirstMatch);

        var segment = Assert.Single(keep);
        Assert.Equal(TimeSpan.Zero, segment.Start);
        Assert.Equal(start, segment.End);
    }

    [Fact]
    public void CutBefore_StartsAtMatchEnd()
    {
        var matchStart = TimeSpan.FromSeconds(90);
        var matchEnd = TimeSpan.FromSeconds(100);
        var keep = _planner.Plan(Duration, [TestAudio.Match(File, matchStart, matchEnd)], ProcessingAction.CutBeforeFirstMatch);
        var segment = Assert.Single(keep);
        Assert.Equal(matchEnd, segment.Start);
        Assert.Equal(Duration, segment.End);
        Assert.NotEqual(matchStart, segment.Start);
    }

    [Fact]
    public void CutBefore_MatchAtEnd_ProducesNoOutput()
    {
        var keep = _planner.Plan(Duration, [TestAudio.Match(File, TimeSpan.FromMinutes(9), Duration)], ProcessingAction.CutBeforeFirstMatch);
        Assert.Empty(keep);
    }

    [Fact]
    public void CutAfter_MatchAtBeginning_ProducesNoOutput()
    {
        var keep = _planner.Plan(Duration, [TestAudio.Match(File, TimeSpan.Zero, TimeSpan.FromSeconds(12))], ProcessingAction.CutAfterFirstMatch);
        Assert.Empty(keep);
    }

    [Fact]
    public void CutAfter_StopsAtMatchStart()
    {
        var matchStart = TimeSpan.FromSeconds(90);
        var matchEnd = TimeSpan.FromSeconds(100);
        var keep = _planner.Plan(Duration, [TestAudio.Match(File, matchStart, matchEnd)], ProcessingAction.CutAfterFirstMatch);
        var segment = Assert.Single(keep);
        Assert.Equal(TimeSpan.Zero, segment.Start);
        Assert.Equal(matchStart, segment.End);
        Assert.NotEqual(matchEnd, segment.End);
    }

    [Fact]
    public void CutActions_UseFirstMatchInclusiveRange()
    {
        var matchStart = TimeSpan.Parse("00:19:54.077");
        var matchEnd = TimeSpan.Parse("00:20:00.183");
        var duration = TimeSpan.FromMinutes(30);
        var matches = new[] { TestAudio.Match(File, matchStart, matchEnd) };

        var before = Assert.Single(_planner.Plan(duration, matches, ProcessingAction.CutBeforeFirstMatch));
        Assert.Equal(matchEnd, before.Start);
        Assert.Equal(duration, before.End);

        var after = Assert.Single(_planner.Plan(duration, matches, ProcessingAction.CutAfterFirstMatch));
        Assert.Equal(TimeSpan.Zero, after.Start);
        Assert.Equal(matchStart, after.End);
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
        Assert.Equal(TimeSpan.FromMinutes(2.2), before.Start);
        Assert.Equal(Duration, before.End);

        var after = Assert.Single(_planner.Plan(Duration, matches, ProcessingAction.CutAfterFirstMatch));
        Assert.Equal(TimeSpan.FromMinutes(2), after.End);
    }
}
