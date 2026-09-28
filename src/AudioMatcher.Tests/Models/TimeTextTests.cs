using AudioMatcher.Core.Models;

namespace AudioMatcher.Tests.Models;

public sealed class TimeTextTests
{
    [Fact]
    public void RoundTrips()
    {
        var time = new TimeSpan(0, 1, 23, 35, 500);
        var text = TimeText.Format(time);
        Assert.Equal("01:23:35.500", text);
        Assert.True(TimeText.TryParse(text, out var parsed));
        Assert.Equal(time, parsed);
    }
}
