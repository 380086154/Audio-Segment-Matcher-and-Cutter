namespace AudioMatcher.Core.Models;

public sealed class AudioSample
{
    public AudioSample(string sourceFilePath, TimeSpan start, TimeSpan end)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
        {
            throw new ArgumentException("Source file path is required.", nameof(sourceFilePath));
        }

        if (start < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "Sample start cannot be negative.");
        }

        if (end <= start)
        {
            throw new ArgumentOutOfRangeException(nameof(end), "Sample end must be greater than start.");
        }

        SourceFilePath = sourceFilePath;
        Start = start;
        End = end;
    }

    public string SourceFilePath { get; }

    public TimeSpan Start { get; }

    public TimeSpan End { get; }

    public TimeSpan Duration => End - Start;

    public bool IsShort => Duration < TimeSpan.FromSeconds(3);
}
