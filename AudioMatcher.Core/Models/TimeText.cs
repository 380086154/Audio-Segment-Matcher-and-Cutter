using System.Globalization;

namespace AudioMatcher.Core.Models;

public static class TimeText
{
    private static readonly string[] Formats =
    [
        @"hh\:mm\:ss\.fff",
        @"hh\:mm\:ss\.ff",
        @"hh\:mm\:ss\.f",
        @"hh\:mm\:ss",
        @"mm\:ss\.fff",
        @"mm\:ss"
    ];

    public static string Format(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
        {
            time = TimeSpan.Zero;
        }

        return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}.{time.Milliseconds:000}";
    }

    public static bool TryParse(string? text, out TimeSpan time)
    {
        time = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        text = text.Trim();
        if (TimeSpan.TryParseExact(text, Formats, CultureInfo.InvariantCulture, out time))
        {
            return true;
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0)
        {
            time = TimeSpan.FromSeconds(seconds);
            return true;
        }

        return TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out time);
    }
}
