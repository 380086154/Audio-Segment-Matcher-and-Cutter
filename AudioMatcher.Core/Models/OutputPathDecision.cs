namespace AudioMatcher.Core.Models;

public enum OutputPathKind
{
    Write,
    SkipExisting,
    RefuseOverwriteOriginal
}

public sealed class OutputPathDecision
{
    private OutputPathDecision(OutputPathKind kind, string outputPath, string? reason = null)
    {
        Kind = kind;
        OutputPath = outputPath;
        Reason = reason;
    }

    public OutputPathKind Kind { get; }

    public string OutputPath { get; }

    public string? Reason { get; }

    public bool CanWrite => Kind == OutputPathKind.Write;

    public static OutputPathDecision Write(string outputPath) => new(OutputPathKind.Write, outputPath);

    public static OutputPathDecision SkipExisting(string outputPath)
        => new(OutputPathKind.SkipExisting, outputPath, "Output file already exists.");

    public static OutputPathDecision RefuseOverwriteOriginal(string outputPath)
        => new(OutputPathKind.RefuseOverwriteOriginal, outputPath, "Refusing to overwrite the original audio file.");
}
