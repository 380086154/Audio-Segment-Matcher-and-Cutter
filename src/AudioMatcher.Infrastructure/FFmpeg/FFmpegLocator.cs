namespace AudioMatcher.Infrastructure.FFmpeg;

public sealed class FFmpegLocator
{
    public string? FfmpegPath { get; private set; }

    public string? FfprobePath { get; private set; }

    public bool IsAvailable => File.Exists(FfmpegPath);

    public static string SavedPathFile { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Audio Segment Matcher",
        "ffmpeg.path");

    public FFmpegLocator()
    {
        var saved = ReadSavedPath();
        FfmpegPath = saved ?? Find("ffmpeg.exe");
        FfprobePath = FindProbeNear(FfmpegPath) ?? Find("ffprobe.exe");
    }

    public bool TryUse(string ffmpegPath)
    {
        if (string.IsNullOrWhiteSpace(ffmpegPath) || !File.Exists(ffmpegPath))
        {
            return false;
        }

        FfmpegPath = Path.GetFullPath(ffmpegPath);
        FfprobePath = FindProbeNear(FfmpegPath);
        var directory = Path.GetDirectoryName(SavedPathFile);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(SavedPathFile, FfmpegPath);
        }

        return true;
    }

    public string RequireFfmpeg()
        => FfmpegPath ?? throw new InvalidOperationException(MissingMessage);

    public string RequireFfprobe()
        => FfprobePath ?? throw new InvalidOperationException(MissingMessage);

    public static string MissingMessage =>
        "FFmpeg was not found. Click Locate FFmpeg and select ffmpeg.exe, or place it in tools\\ffmpeg / PATH.";

    private static string? FindProbeNear(string? ffmpegPath)
    {
        if (string.IsNullOrWhiteSpace(ffmpegPath))
        {
            return null;
        }

        var directory = Path.GetDirectoryName(ffmpegPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        var probe = Path.Combine(directory, "ffprobe.exe");
        return File.Exists(probe) ? probe : null;
    }

    private static string? ReadSavedPath()
    {
        try
        {
            if (!File.Exists(SavedPathFile))
            {
                return null;
            }

            var path = File.ReadAllText(SavedPathFile).Trim();
            return File.Exists(path) ? path : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? Find(string fileName)
    {
        foreach (var directory in CandidateDirectories())
        {
            var path = Path.Combine(directory, fileName);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return FindOnPath(fileName);
    }

    private static IEnumerable<string> CandidateDirectories()
    {
        var baseDir = AppContext.BaseDirectory;
        yield return baseDir;
        yield return Path.Combine(baseDir, "ffmpeg");
        yield return Path.Combine(baseDir, "tools", "ffmpeg");
        yield return Path.Combine(baseDir, "tools");

        var workspace = Directory.GetCurrentDirectory();
        yield return Path.Combine(workspace, "tools", "ffmpeg");
        yield return @"C:\ffmpeg\bin";
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ffmpeg", "bin");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Audio Segment Matcher", "ffmpeg");
    }

    private static string? FindOnPath(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(directory.Trim(), fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch (Exception)
            {
                // Ignore invalid PATH entries.
            }
        }

        return null;
    }
}
