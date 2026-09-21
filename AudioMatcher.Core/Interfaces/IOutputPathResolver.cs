using AudioMatcher.Core.Models;

namespace AudioMatcher.Core.Interfaces;

public interface IOutputPathResolver
{
    OutputPathDecision Resolve(
        string sourceFilePath,
        string searchRoot,
        string outputFolder,
        OutputConflictPolicy policy);
}
