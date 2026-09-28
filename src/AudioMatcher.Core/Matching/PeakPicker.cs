namespace AudioMatcher.Core.Matching;

internal readonly record struct CorrelationPeak(int Index, float Value);

internal static class PeakPicker
{
    public static IReadOnlyList<CorrelationPeak> Pick(float[] values, double threshold, int minSeparation)
    {
        if (values.Length == 0)
        {
            return [];
        }

        minSeparation = Math.Max(1, minSeparation);
        var candidates = new List<CorrelationPeak>();
        for (var i = 0; i < values.Length; i++)
        {
            var value = values[i];
            if (value < threshold)
            {
                continue;
            }

            var left = i > 0 ? values[i - 1] : float.NegativeInfinity;
            var right = i + 1 < values.Length ? values[i + 1] : float.NegativeInfinity;
            if (value >= left && value >= right)
            {
                candidates.Add(new CorrelationPeak(i, value));
            }
        }

        candidates.Sort((a, b) => b.Value.CompareTo(a.Value));
        var accepted = new List<CorrelationPeak>();
        foreach (var peak in candidates)
        {
            var tooClose = false;
            foreach (var existing in accepted)
            {
                if (Math.Abs(existing.Index - peak.Index) < minSeparation)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
            {
                accepted.Add(peak);
            }
        }

        accepted.Sort((a, b) => a.Index.CompareTo(b.Index));
        return accepted;
    }
}
