namespace AudioMatcher.Core.Models;

public static class AudioFileFormats
{
    public static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3",
        ".wav",
        ".flac",
        ".m4a",
        ".aac",
        ".ogg",
        ".wma"
    };

    public static bool IsSupported(string filePath)
        => Extensions.Contains(Path.GetExtension(filePath));
}
