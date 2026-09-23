using AudioMatcher.Core.Models;
using AudioMatcher.Infrastructure.FFmpeg;

namespace AudioMatcher.Tests.Infrastructure;

public sealed class FFmpegAudioProcessorTests
{
    [Fact]
    public void SingleSegment_CopiesAudioStreamWithoutReencoding()
    {
        var args = FFmpegAudioProcessor.BuildArguments(
            @"D:\in\chapter.mp3",
            @"D:\out\chapter.mp3",
            new KeepSegment(TimeSpan.FromSeconds(12.5), TimeSpan.FromSeconds(90)));

        Assert.Contains("-ss", args);
        Assert.Contains("12.5", args);
        var tIndex = args.IndexOf("-t");
        Assert.True(tIndex >= 0);
        Assert.Equal("77.5", args[tIndex + 1]);
        Assert.DoesNotContain("-to", args);
        Assert.DoesNotContain("-filter_complex", args);
        var copyIndex = args.IndexOf("-c");
        Assert.True(copyIndex >= 0);
        Assert.Equal("copy", args[copyIndex + 1]);
        Assert.DoesNotContain("libmp3lame", args);
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
    public void ConcatList_WritesQuotedFileEntries()
    {
        var list = FFmpegAudioProcessor.BuildConcatList(
        [
            @"D:\out\chapter.seg0.partial.mp3",
            @"D:\out\chapter.seg1.partial.mp3"
        ]);

        Assert.Contains("file '", list);
        Assert.Contains("chapter.seg0.partial.mp3", list);
        Assert.Contains("chapter.seg1.partial.mp3", list);
    }

    [Fact]
    public void ConcatArguments_CopyStream()
    {
        var args = FFmpegAudioProcessor.BuildConcatArguments(@"D:\out\chapter.concat.txt", @"D:\out\chapter.mp3");
        Assert.Contains("-f", args);
        Assert.Contains("concat", args);
        var copyIndex = args.IndexOf("-c");
        Assert.True(copyIndex >= 0);
        Assert.Equal("copy", args[copyIndex + 1]);
        Assert.DoesNotContain("libmp3lame", args);
    }
}
