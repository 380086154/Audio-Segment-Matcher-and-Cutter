using AudioMatcher.Core.Interfaces;
using AudioMatcher.Core.Models;

namespace AudioMatcher.Core.Matching;

public sealed class NormalizedCrossCorrelationMatcher : IAudioMatcher
{
    public IReadOnlyList<AudioMatch> FindMatches(
        PcmAudio sample,
        PcmAudio target,
        string targetFilePath,
        MatchOptions options)
    {
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(options);

        var sampleRate = options.AnalysisSampleRate;
        var sampleMono = AudioMonoResampler.ToMonoResampled(sample, sampleRate);
        var targetMono = AudioMonoResampler.ToMonoResampled(target, sampleRate);
        if (sampleMono.Length == 0 || targetMono.Length < sampleMono.Length)
        {
            return [];
        }

        var ncc = FftCrossCorrelation.ZeroMeanNormalized(sampleMono, targetMono);
        var minSeparation = Math.Max(1, (int)(sampleMono.Length * options.MinPeakSeparationRatio));
        var peaks = PeakPicker.Pick(ncc, options.MinConfidence, minSeparation);
        var matches = new AudioMatch[peaks.Count];
        for (var i = 0; i < peaks.Count; i++)
        {
            var peak = peaks[i];
            var start = TimeSpan.FromSeconds(peak.Index / (double)sampleRate);
            var end = TimeSpan.FromSeconds((peak.Index + sampleMono.Length) / (double)sampleRate);
            matches[i] = new AudioMatch(targetFilePath, start, end, peak.Value);
        }

        return matches;
    }
}
