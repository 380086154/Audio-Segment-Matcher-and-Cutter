using AudioMatcher.Core.Models;

namespace AudioMatcher.Tests.Models;

public sealed class AudioSampleTests
{
    [Fact]
    public void ShortSample_IsFlaggedButStillValid()
    {
        var sample = new AudioSample("a.mp3", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2.5));
        Assert.True(sample.IsShort);
        Assert.Equal(TimeSpan.FromSeconds(1.5), sample.Duration);
    }
}
