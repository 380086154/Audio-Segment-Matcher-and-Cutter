using AudioMatcher.Core.Interfaces;
using AudioMatcher.Core.Models;

namespace AudioMatcher.Infrastructure.Logging;

public sealed class FileProcessingLogger : IProcessingLogger
{
    private readonly Lock _gate = new();

    public FileProcessingLogger(string? directory = null)
    {
        DirectoryPath = directory ?? Path.Combine(AppContext.BaseDirectory, "logs");
    }

    public string DirectoryPath { get; }

    public void Log(ProcessingFileResult result)
    {
        Directory.CreateDirectory(DirectoryPath);
        var path = Path.Combine(DirectoryPath, $"{DateTime.Now:yyyyMMdd}.log");
        var lines =
            $"""
            [{DateTime.Now:yyyy-MM-dd HH:mm:ss}]
            File: {result.SourceFilePath}
            Action: {result.Action}
            Match Count: {result.MatchCount}
            First Match: {(result.FirstMatchStart is { } start ? TimeText.Format(start) : "--")}
            Output: {result.OutputFilePath ?? "--"}
            Status: {result.Status}
            Message: {result.Message ?? ""}

            """;

        lock (_gate)
        {
            File.AppendAllText(path, lines);
        }
    }
}
