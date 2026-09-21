using AudioMatcher.Core.Models;

namespace AudioMatcher.Core.Interfaces;

public interface IProcessingLogger
{
    string DirectoryPath { get; }

    void Log(ProcessingFileResult result);
}
