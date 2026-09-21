using AudioMatcher.Core.Models;
using AudioMatcher.Core.Processing;
using AudioMatcher.Infrastructure.FFmpeg;

namespace AudioMatcher.Tests.Infrastructure;

public sealed class FFmpegAudioProcessorTests
{
    [Fact]
    public void SingleSegment_UsesAccurateSeekWithoutFilterGraph()
    {
        var args = FFmpegAudioProcessor.BuildArguments(
            @"D:\in\chapter.mp3",
            @"D:\out\chapter.mp3",
            [new KeepSegment(TimeSpan.FromSeconds(12.5), TimeSpan.FromSeconds(90))]);

        Assert.Contains("-ss", args);
        Assert.Contains("12.5", args);
        Assert.Contains("-to", args);
        Assert.DoesNotContain("-filter_complex", args);
        Assert.Contains("libmp3lame", args);
        Assert.Contains("-f", args);
        Assert.Contains("mp3", args);
    }

    [Fact]
    public void TempOutputPath_KeepsAudioExtension()
    {
        var temp = FFmpegAudioProcessor.CreateTempOutputPath(@"D:\out\chapter.mp3");
        Assert.Equal(@"D:\out\chapter.partial.mp3", temp);
        Assert.Equal(".mp3", Path.GetExtension(temp));
    }

    [Fact]
    public void MultipleSegments_UseConcatFilter()
    {
        var filter = FFmpegAudioProcessor.BuildFilter(
        [
            new KeepSegment(TimeSpan.Zero, TimeSpan.FromSeconds(10)),
            new KeepSegment(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(30))
        ]);

        Assert.Contains("atrim=start=0:end=10", filter);
        Assert.Contains("atrim=start=20:end=30", filter);
        Assert.Contains("concat=n=2:v=0:a=1[out]", filter);
    }
}
