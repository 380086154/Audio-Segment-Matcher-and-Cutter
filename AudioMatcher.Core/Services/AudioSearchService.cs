using AudioMatcher.Core.Interfaces;
using AudioMatcher.Core.Models;

namespace AudioMatcher.Core.Services;

public sealed class AudioSearchService : IAudioSearchService
{
    private readonly IAudioDecoder _decoder;
    private readonly IAudioMatcher _matcher;
    private readonly IAudioFileScanner _scanner;

    public AudioSearchService(IAudioDecoder decoder, IAudioMatcher matcher, IAudioFileScanner scanner)
    {
        _decoder = decoder;
        _matcher = matcher;
        _scanner = scanner;
    }

    public async Task<IReadOnlyList<AudioMatchResult>> SearchAsync(
        AudioSample sample,
        SearchRequest request,
        IProgress<SearchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentNullException.ThrowIfNull(request);

        var files = _scanner.Scan(request.FolderPath, request.IncludeSubfolders);
        progress?.Report(new SearchProgress(0, files.Count, 0, null));
        if (files.Count == 0)
        {
            return [];
        }

        var options = request.MatchOptions;
        var samplePcm = await _decoder.DecodeSegmentAsync(
            sample.SourceFilePath,
            sample.Start,
            sample.End,
            options.AnalysisSampleRate,
            1,
            cancellationToken).ConfigureAwait(false);

        var results = new AudioMatchResult?[files.Count];
        var processed = 0;
        var matched = 0;
        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Math.Max(1, options.MaxDegreeOfParallelism)
        };

        try
        {
            await Parallel.ForEachAsync(Enumerable.Range(0, files.Count), parallelOptions, async (index, token) =>
            {
                var file = files[index];
                AudioMatchResult result;
                try
                {
                    var info = await _decoder.GetInfoAsync(file, token).ConfigureAwait(false);
                    var pcm = await _decoder.DecodeAsync(file, options.AnalysisSampleRate, 1, token).ConfigureAwait(false);
                    var matches = _matcher.FindMatches(samplePcm, pcm, file, options);
                    result = matches.Count == 0
                        ? AudioMatchResult.None(file, info.Duration)
                        : AudioMatchResult.Matched(file, info.Duration, matches);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    result = AudioMatchResult.Failed(file, ex.Message);
                }

                results[index] = result;
                var currentProcessed = Interlocked.Increment(ref processed);
                var currentMatched = result.HasMatch ? Interlocked.Increment(ref matched) : Volatile.Read(ref matched);
                progress?.Report(new SearchProgress(currentProcessed, files.Count, currentMatched, file));
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Keep completed files. Incomplete work is discarded without output.
        }

        return results.OfType<AudioMatchResult>().ToArray();
    }
}
