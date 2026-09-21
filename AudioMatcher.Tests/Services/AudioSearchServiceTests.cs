using AudioMatcher.Core.Interfaces;
using AudioMatcher.Core.Matching;
using AudioMatcher.Core.Models;
using AudioMatcher.Core.Services;
using AudioMatcher.Tests.Support;

namespace AudioMatcher.Tests.Services;

public sealed class AudioSearchServiceTests
{
    [Fact]
    public async Task SearchesMultipleFiles_AndKeepsCompletedResultsAfterCancel()
    {
        var sampleAudio = TestAudio.Noise(TimeSpan.FromSeconds(3), 8000, 1);
        var host = TestAudio.Embed(TestAudio.Noise(TimeSpan.FromSeconds(12), 8000, 2), sampleAudio, TimeSpan.FromSeconds(4));
        var decoder = new FakeDecoder
        {
            Files =
            {
                ["source.mp3"] = sampleAudio,
                ["a.mp3"] = host,
                ["b.mp3"] = TestAudio.Noise(TimeSpan.FromSeconds(12), 8000, 9)
            }
        };
        var scanner = new FakeScanner(["a.mp3", "b.mp3"]);
        var service = new AudioSearchService(decoder, new NormalizedCrossCorrelationMatcher(), scanner);

        var results = await service.SearchAsync(
            new AudioSample("source.mp3", TimeSpan.Zero, TimeSpan.FromSeconds(3)),
            new SearchRequest { FolderPath = "in", MatchOptions = MatchOptions.Normal with { MaxDegreeOfParallelism = 1, AnalysisSampleRate = 8000 } });

        Assert.Equal(2, results.Count);
        Assert.True(results.Single(result => result.FilePath == "a.mp3").HasMatch);
        Assert.False(results.Single(result => result.FilePath == "b.mp3").HasMatch);
    }

    private sealed class FakeDecoder : IAudioDecoder
    {
        public Dictionary<string, PcmAudio> Files { get; } = new();

        public Task<AudioInfo> GetInfoAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var pcm = Files[filePath];
            return Task.FromResult(new AudioInfo(filePath, pcm.Duration, pcm.SampleRate, pcm.Channels));
        }

        public Task<PcmAudio> DecodeAsync(string filePath, int sampleRate, int channels = 1, CancellationToken cancellationToken = default)
            => Task.FromResult(Files[filePath]);

        public Task<PcmAudio> DecodeSegmentAsync(
            string filePath,
            TimeSpan start,
            TimeSpan end,
            int sampleRate,
            int channels = 1,
            CancellationToken cancellationToken = default)
            => Task.FromResult(filePath == "source.mp3" ? Files["source.mp3"] : TestAudio.Slice(Files[filePath], start, end));
    }

    private sealed class FakeScanner(IReadOnlyList<string> files) : IAudioFileScanner
    {
        public IReadOnlyList<string> Scan(string folderPath, bool includeSubfolders) => files;
    }
}
