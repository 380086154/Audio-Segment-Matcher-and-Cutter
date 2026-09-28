using AudioMatcher.Core.Models;

namespace AudioMatcher.Core.Matching;

public static class FirstMatchSelector
{
    public static AudioMatch? Select(IEnumerable<AudioMatch>? matches)
    {
        if (matches is null)
        {
            return null;
        }

        AudioMatch? first = null;
        foreach (var match in matches)
        {
            if (first is null || match.Start < first.Start || (match.Start == first.Start && match.End < first.End))
            {
                first = match;
            }
        }

        return first;
    }
}
