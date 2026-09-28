using AudioMatcher.Core.Matching;
using AudioMatcher.Core.Models;
using AudioMatcher.Tests.Support;

namespace AudioMatcher.Tests.Matching;

public sealed class NormalizedCrossCorrelationMatcherTests
{
    private const int SampleRate = 8000;
    private readonly NormalizedCrossCorrelationMatcher _matcher = new();
    private readonly MatchOptions _options = MatchOptions.Normal with { AnalysisSampleRate = SampleRate };

    [Fact]
    public void IdenticalNoise_FindsMatch()
    {
        var snippet = TestAudio.Noise(TimeSpan.FromSeconds(4), SampleRate, seed: 7);
        var host = TestAudio.Embed(
            TestAudio.Noise(TimeSpan.FromSeconds(20), SampleRate, seed: 99),
            snippet,
            TimeSpan.FromSeconds(6));

        var matches = _matcher.FindMatches(snippet, host, "chapter.mp3", _options);

        var match = Assert.Single(matches);
        Assert.InRange(match.Start.TotalSeconds, 5.95, 6.05);
        Assert.True(match.Confidence >= 0.95, $"Confidence was {match.Confidence}");
    }

    [Fact]
    public void SampleDoesNotExist_ReturnsEmpty()
    {
        var snippet = TestAudio.Noise(TimeSpan.FromSeconds(4), SampleRate, seed: 3);
        var host = TestAudio.Noise(TimeSpan.FromSeconds(20), SampleRate, seed: 4);

        var matches = _matcher.FindMatches(snippet, host, "chapter.mp3", _options);

        Assert.Empty(matches);
    }

    [Fact]
    public void MultipleMatches_AreAllReturned()
    {
        var snippet = TestAudio.Noise(TimeSpan.FromSeconds(3), SampleRate, seed: 21);
        var host = TestAudio.Noise(TimeSpan.FromSeconds(40), SampleRate, seed: 22);
        host = TestAudio.Embed(host, snippet, TimeSpan.FromSeconds(4));
        host = TestAudio.Embed(host, snippet, TimeSpan.FromSeconds(16));
        host = TestAudio.Embed(host, snippet, TimeSpan.FromSeconds(30));

        var matches = _matcher.FindMatches(snippet, host, "chapter.mp3", _options);

        Assert.Equal(3, matches.Count);
        Assert.Equal([4, 16, 30], matches.Select(match => Math.Round(match.Start.TotalSeconds)).ToArray());
    }

    [Fact]
    public void VolumeChange_StillMatches()
    {
        var snippet = TestAudio.Noise(TimeSpan.FromSeconds(4), SampleRate, seed: 11, amplitude: 1);
        var host = TestAudio.Embed(
            TestAudio.Noise(TimeSpan.FromSeconds(18), SampleRate, seed: 12, amplitude: 0.2),
            snippet,
            TimeSpan.FromSeconds(8),
            gain: 0.35);

        var matches = _matcher.FindMatches(snippet, host, "chapter.mp3", _options);

        var match = Assert.Single(matches);
        Assert.InRange(match.Start.TotalSeconds, 7.95, 8.05);
    }

    [Fact]
    public void SlightNoise_StillMatches()
    {
        var snippet = TestAudio.Noise(TimeSpan.FromSeconds(4), SampleRate, seed: 41);
        var host = TestAudio.Embed(
            TestAudio.Noise(TimeSpan.FromSeconds(16), SampleRate, seed: 42),
            snippet,
            TimeSpan.FromSeconds(5));
        var noisy = new float[host.Samples.Length];
        var rng = new Random(100);
        for (var i = 0; i < noisy.Length; i++)
        {
            noisy[i] = host.Samples[i] + (float)((rng.NextDouble() * 2 - 1) * 0.02);
        }

        var matches = _matcher.FindMatches(snippet, new PcmAudio(noisy, SampleRate, 1), "chapter.mp3", _options);

        var match = Assert.Single(matches);
        Assert.InRange(match.Start.TotalSeconds, 4.95, 5.05);
    }

    [Fact]
    public void MatchAtBeginning()
    {
        var snippet = TestAudio.Noise(TimeSpan.FromSeconds(3), SampleRate, seed: 51);
        var host = TestAudio.Embed(
            TestAudio.Noise(TimeSpan.FromSeconds(12), SampleRate, seed: 52),
            snippet,
            TimeSpan.Zero);

        var match = Assert.Single(_matcher.FindMatches(snippet, host, "chapter.mp3", _options));
        Assert.InRange(match.Start.TotalSeconds, 0, 0.05);
    }

    [Fact]
    public void MatchAtEnd()
    {
        var snippet = TestAudio.Noise(TimeSpan.FromSeconds(3), SampleRate, seed: 61);
        var hostDuration = TimeSpan.FromSeconds(15);
        var at = hostDuration - snippet.Duration;
        var host = TestAudio.Embed(
            TestAudio.Noise(hostDuration, SampleRate, seed: 62),
            snippet,
            at);

        var match = Assert.Single(_matcher.FindMatches(snippet, host, "chapter.mp3", _options));
        Assert.InRange(match.Start.TotalSeconds, at.TotalSeconds - 0.05, at.TotalSeconds + 0.05);
    }

    [Fact]
    public void SimilarButDifferentSound_DoesNotMatch()
    {
        var snippet = TestAudio.Noise(TimeSpan.FromSeconds(4), SampleRate, seed: 71);
        var similar = TestAudio.Noise(TimeSpan.FromSeconds(12), SampleRate, seed: 72);

        Assert.Empty(_matcher.FindMatches(snippet, similar, "chapter.mp3", _options));
    }

    [Fact]
    public void VeryShortSample_CanStillBeSearched()
    {
        var snippet = TestAudio.Noise(TimeSpan.FromMilliseconds(400), SampleRate, seed: 81);
        var host = TestAudio.Embed(
            TestAudio.Noise(TimeSpan.FromSeconds(6), SampleRate, seed: 82),
            snippet,
            TimeSpan.FromSeconds(2));

        var matches = _matcher.FindMatches(snippet, host, "chapter.mp3", MatchOptions.Relaxed with { AnalysisSampleRate = SampleRate });
        Assert.NotEmpty(matches);
    }

    [Fact]
    public void SelfCorrelationPeak_IsNearOne()
    {
        var snippet = TestAudio.Noise(TimeSpan.FromSeconds(2), SampleRate, seed: 1).Samples;
        var ncc = FftCrossCorrelation.ZeroMeanNormalized(snippet, snippet);
        Assert.True(ncc[0] > 0.99, $"NCC at zero lag was {ncc[0]}");
    }
}
