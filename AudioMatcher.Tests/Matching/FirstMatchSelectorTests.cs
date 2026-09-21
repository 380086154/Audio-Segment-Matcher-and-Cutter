using AudioMatcher.Core.Matching;
using AudioMatcher.Core.Models;
using AudioMatcher.Tests.Support;

namespace AudioMatcher.Tests.Matching;

public sealed class FirstMatchSelectorTests
{
    [Fact]
    public void SelectsEarliestAudioPosition_RegardlessOfListOrder()
    {
        var matches = new List<AudioMatch>
        {
            TestAudio.Match("a.mp3", TimeSpan.FromMinutes(45), TimeSpan.FromMinutes(45.2)),
            TestAudio.Match("a.mp3", TimeSpan.FromSeconds(150), TimeSpan.FromSeconds(162)),
            TestAudio.Match("a.mp3", TimeSpan.FromHours(1).Add(TimeSpan.FromMinutes(10)), TimeSpan.FromHours(1).Add(TimeSpan.FromMinutes(10.2)))
        };

        var shuffled = matches.OrderBy(_ => Random.Shared.Next()).ToList();
        var first = FirstMatchSelector.Select(shuffled);

        Assert.NotNull(first);
        Assert.Equal(TimeSpan.FromSeconds(150), first.Start);
    }

    [Fact]
    public void EmptyList_ReturnsNull()
    {
        Assert.Null(FirstMatchSelector.Select([]));
        Assert.Null(FirstMatchSelector.Select(null));
    }

    [Fact]
    public void AudioMatchResult_UsesEarliestMatch()
    {
        var result = AudioMatchResult.Matched("a.mp3", TimeSpan.FromHours(2),
        [
            TestAudio.Match("a.mp3", TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10.2)),
            TestAudio.Match("a.mp3", TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1.2)),
            TestAudio.Match("a.mp3", TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(3.2))
        ]);

        Assert.Equal(TimeSpan.FromMinutes(1), result.FirstMatch!.Start);
    }
}
