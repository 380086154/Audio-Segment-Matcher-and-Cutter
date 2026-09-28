using AudioMatcher.Core.Models;

namespace AudioMatcher.Core.Interfaces;

public interface IAudioMatcher
{
    IReadOnlyList<AudioMatch> FindMatches(
        PcmAudio sample,
        PcmAudio target,
        string targetFilePath,
        MatchOptions options);
}
