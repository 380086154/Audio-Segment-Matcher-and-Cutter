using System.Globalization;
using AudioMatcher.Core.Interfaces;
using AudioMatcher.Core.Models;
using AudioMatcher.Core.Processing;

namespace AudioMatcher.Infrastructure.FFmpeg;

public sealed class FFmpegAudioProcessor : IAudioProcessor
{
    private readonly FFmpegLocator _locator;
    private readonly IFFmpegProcessRunner _runner;
    private readonly AudioSegmentPlanner _planner;
    private readonly IOutputPathResolver _paths;

    public FFmpegAudioProcessor(
        FFmpegLocator locator,
        IFFmpegProcessRunner runner,
        AudioSegmentPlanner planner,
        IOutputPathResolver paths)
    {
        _locator = locator;
        _runner = runner;
        _planner = planner;
        _paths = paths;
    }

    public async Task<ProcessingFileResult> ProcessAsync(
        ProcessingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var source = Path.GetFullPath(request.SourceFilePath);
        if (!File.Exists(source))
        {
            return ProcessingFileResult.Fail(source, "Source file was not found.");
        }

        var originalInfo = new FileInfo(source);
        var originalLength = originalInfo.Length;
        var originalWriteTime = originalInfo.LastWriteTimeUtc;

        var decision = _paths.Resolve(source, request.SearchRoot, request.OutputFolder, request.ConflictPolicy);
        if (decision.Kind == OutputPathKind.SkipExisting)
        {
            return ProcessingFileResult.Skip(source, decision.OutputPath, decision.Reason ?? "Output file already exists.");
        }

        if (decision.Kind == OutputPathKind.RefuseOverwriteOriginal)
        {
            return ProcessingFileResult.Fail(source, decision.Reason ?? "Refusing to overwrite the original audio file.");
        }

        var segments = _planner.Plan(request.FileDuration, request.Matches, request.Action);
        if (segments.Count == 0)
        {
            return ProcessingFileResult.Fail(source, "No output segments were produced. The file would be empty.");
        }

        var outputDirectory = Path.GetDirectoryName(decision.OutputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var tempPath = CreateTempOutputPath(decision.OutputPath);
        TryDelete(tempPath);

        try
        {
            var arguments = BuildArguments(source, tempPath, segments);
            await _runner.RunToFileAsync(_locator.RequireFfmpeg(), arguments, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(tempPath) || new FileInfo(tempPath).Length == 0)
            {
                TryDelete(tempPath);
                return ProcessingFileResult.Fail(source, "FFmpeg did not produce an output file.");
            }

            if (File.Exists(decision.OutputPath))
            {
                File.Delete(decision.OutputPath);
            }

            File.Move(tempPath, decision.OutputPath);
        }
        catch (OperationCanceledException)
        {
            TryDelete(tempPath);
            throw;
        }
        catch (Exception ex)
        {
            TryDelete(tempPath);
            return ProcessingFileResult.Fail(source, ex.Message);
        }

        var after = new FileInfo(source);
        if (after.Length != originalLength || after.LastWriteTimeUtc != originalWriteTime)
        {
            return ProcessingFileResult.Fail(source, "Original file changed during processing. Output was created, but the source should be verified.");
        }

        return ProcessingFileResult.Success(request, decision.OutputPath);
    }

    internal static List<string> BuildArguments(string source, string output, IReadOnlyList<KeepSegment> segments)
    {
        var arguments = new List<string>
        {
            "-hide_banner", "-nostdin", "-y", "-v", "error",
            "-i", source,
            "-vn"
        };

        if (segments.Count == 1)
        {
            arguments.Add("-map");
            arguments.Add("0:a:0");
            arguments.Add("-ss");
            arguments.Add(ToSeconds(segments[0].Start));
            arguments.Add("-to");
            arguments.Add(ToSeconds(segments[0].End));
        }
        else
        {
            arguments.Add("-filter_complex");
            arguments.Add(BuildFilter(segments));
            arguments.Add("-map");
            arguments.Add("[out]");
        }

        arguments.AddRange(CodecArguments(output));
        arguments.AddRange(ContainerArguments(output));
        arguments.Add(output);
        return arguments;
    }

    internal static string CreateTempOutputPath(string outputPath)
    {
        var directory = Path.GetDirectoryName(outputPath);
        var name = Path.GetFileNameWithoutExtension(outputPath);
        var extension = Path.GetExtension(outputPath);
        var fileName = $"{name}.partial{extension}";
        return string.IsNullOrWhiteSpace(directory) ? fileName : Path.Combine(directory, fileName);
    }

    internal static string BuildFilter(IReadOnlyList<KeepSegment> segments)
    {
        var labels = new List<string>(segments.Count);
        var builder = new System.Text.StringBuilder();
        for (var i = 0; i < segments.Count; i++)
        {
            var label = $"a{i}";
            labels.Add($"[{label}]");
            builder.Append(CultureInfo.InvariantCulture,
                $"[0:a]atrim=start={ToSeconds(segments[i].Start)}:end={ToSeconds(segments[i].End)},asetpts=PTS-STARTPTS[{label}];");
        }

        builder.Append(string.Concat(labels));
        builder.Append(CultureInfo.InvariantCulture, $"concat=n={segments.Count}:v=0:a=1[out]");
        return builder.ToString();
    }

    private static IEnumerable<string> ContainerArguments(string outputPath)
    {
        var format = Path.GetExtension(outputPath).ToLowerInvariant() switch
        {
            ".mp3" => "mp3",
            ".wav" => "wav",
            ".flac" => "flac",
            ".m4a" => "ipod",
            ".aac" => "adts",
            ".ogg" => "ogg",
            _ => null
        };

        return format is null ? [] : ["-f", format];
    }

    private static IEnumerable<string> CodecArguments(string outputPath)
    {
        var extension = Path.GetExtension(outputPath).ToLowerInvariant();
        return extension switch
        {
            ".wav" => ["-c:a", "pcm_s16le"],
            ".flac" => ["-c:a", "flac"],
            ".m4a" or ".aac" => ["-c:a", "aac", "-b:a", "192k"],
            ".ogg" => ["-c:a", "libvorbis", "-q:a", "5"],
            _ => ["-c:a", "libmp3lame", "-q:a", "2"]
        };
    }

    private static string ToSeconds(TimeSpan time)
        => time.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup of incomplete output.
        }
    }
}
