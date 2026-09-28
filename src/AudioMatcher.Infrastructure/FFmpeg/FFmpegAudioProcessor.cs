using System.Globalization;
using System.Text;
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
        var extras = new List<string>();
        TryDelete(tempPath);

        try
        {
            var ffmpeg = _locator.RequireFfmpeg();
            if (segments.Count == 1)
            {
                var arguments = BuildArguments(source, tempPath, segments[0]);
                await _runner.RunToFileAsync(ffmpeg, arguments, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await ConcatCopyAsync(ffmpeg, source, tempPath, decision.OutputPath, segments, extras, cancellationToken)
                    .ConfigureAwait(false);
            }

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
        finally
        {
            foreach (var extra in extras)
            {
                TryDelete(extra);
            }
        }

        var after = new FileInfo(source);
        if (after.Length != originalLength || after.LastWriteTimeUtc != originalWriteTime)
        {
            return ProcessingFileResult.Fail(source, "Original file changed during processing. Output was created, but the source should be verified.");
        }

        return ProcessingFileResult.Success(request, decision.OutputPath);
    }

    internal static List<string> BuildArguments(string source, string output, KeepSegment segment)
    {
        var arguments = new List<string>
        {
            "-hide_banner", "-nostdin", "-y", "-v", "error",
            "-i", source,
            "-vn",
            "-map", "0:a:0",
            "-ss", ToSeconds(segment.Start),
            "-t", ToSeconds(segment.End - segment.Start),
            "-c", "copy",
            "-avoid_negative_ts", "make_zero"
        };

        arguments.AddRange(ContainerArguments(output));
        arguments.Add(output);
        return arguments;
    }

    internal static List<string> BuildConcatArguments(string listPath, string output)
    {
        var arguments = new List<string>
        {
            "-hide_banner", "-nostdin", "-y", "-v", "error",
            "-f", "concat",
            "-safe", "0",
            "-i", listPath,
            "-c", "copy",
            "-avoid_negative_ts", "make_zero"
        };

        arguments.AddRange(ContainerArguments(output));
        arguments.Add(output);
        return arguments;
    }

    internal static string BuildConcatList(IReadOnlyList<string> files)
    {
        var builder = new StringBuilder();
        foreach (var file in files)
        {
            var path = Path.GetFullPath(file).Replace('\\', '/').Replace("'", @"'\''");
            builder.Append("file '");
            builder.Append(path);
            builder.AppendLine("'");
        }

        return builder.ToString();
    }

    internal static string CreateTempOutputPath(string outputPath)
    {
        var directory = Path.GetDirectoryName(outputPath);
        var name = Path.GetFileNameWithoutExtension(outputPath);
        var extension = Path.GetExtension(outputPath);
        var fileName = $"{name}.partial{extension}";
        return string.IsNullOrWhiteSpace(directory) ? fileName : Path.Combine(directory, fileName);
    }

    internal static string CreateSidecarPath(string outputPath, string suffix)
    {
        var directory = Path.GetDirectoryName(outputPath);
        var name = Path.GetFileNameWithoutExtension(outputPath);
        var fileName = name + suffix;
        return string.IsNullOrWhiteSpace(directory) ? fileName : Path.Combine(directory, fileName);
    }

    private async Task ConcatCopyAsync(
        string ffmpeg,
        string source,
        string output,
        string finalOutputPath,
        IReadOnlyList<KeepSegment> segments,
        List<string> extras,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(finalOutputPath);
        var partPaths = new List<string>(segments.Count);
        for (var i = 0; i < segments.Count; i++)
        {
            var partPath = CreateSidecarPath(finalOutputPath, $".seg{i}.partial{extension}");
            extras.Add(partPath);
            partPaths.Add(partPath);
            TryDelete(partPath);
            var arguments = BuildArguments(source, partPath, segments[i]);
            await _runner.RunToFileAsync(ffmpeg, arguments, cancellationToken).ConfigureAwait(false);
        }

        var listPath = CreateSidecarPath(finalOutputPath, ".concat.txt");
        extras.Add(listPath);
        await File.WriteAllTextAsync(listPath, BuildConcatList(partPaths), cancellationToken).ConfigureAwait(false);
        await _runner.RunToFileAsync(ffmpeg, BuildConcatArguments(listPath, output), cancellationToken).ConfigureAwait(false);
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
