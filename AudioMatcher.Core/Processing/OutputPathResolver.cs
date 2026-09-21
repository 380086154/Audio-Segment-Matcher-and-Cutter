using AudioMatcher.Core.Interfaces;
using AudioMatcher.Core.Models;

namespace AudioMatcher.Core.Processing;

public sealed class OutputPathResolver : IOutputPathResolver
{
    public OutputPathDecision Resolve(
        string sourceFilePath,
        string searchRoot,
        string outputFolder,
        OutputConflictPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
        {
            throw new ArgumentException("Source file path is required.", nameof(sourceFilePath));
        }

        if (string.IsNullOrWhiteSpace(outputFolder))
        {
            throw new ArgumentException("Output folder is required.", nameof(outputFolder));
        }

        var sourceFull = Path.GetFullPath(sourceFilePath);
        var relative = GetRelativeOutputPath(sourceFull, searchRoot);
        var outputFull = Path.GetFullPath(Path.Combine(outputFolder, relative));

        if (string.Equals(sourceFull, outputFull, StringComparison.OrdinalIgnoreCase))
        {
            return OutputPathDecision.RefuseOverwriteOriginal(outputFull);
        }

        if (File.Exists(outputFull) && policy == OutputConflictPolicy.Skip)
        {
            return OutputPathDecision.SkipExisting(outputFull);
        }

        return OutputPathDecision.Write(outputFull);
    }

    private static string GetRelativeOutputPath(string sourceFull, string? searchRoot)
    {
        if (!string.IsNullOrWhiteSpace(searchRoot))
        {
            var rootFull = Path.GetFullPath(searchRoot);
            var relative = Path.GetRelativePath(rootFull, sourceFull);
            if (!relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative))
            {
                return relative;
            }
        }

        return Path.GetFileName(sourceFull);
    }
}
