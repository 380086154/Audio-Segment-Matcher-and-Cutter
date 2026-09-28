namespace AudioMatcher.Core.Interfaces;

public interface IAudioFileScanner
{
    IReadOnlyList<string> Scan(string folderPath, bool includeSubfolders);
}
