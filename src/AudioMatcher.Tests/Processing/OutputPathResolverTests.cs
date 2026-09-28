using AudioMatcher.Core.Models;
using AudioMatcher.Core.Processing;

namespace AudioMatcher.Tests.Processing;

public sealed class OutputPathResolverTests
{
    private readonly OutputPathResolver _resolver = new();

    [Fact]
    public void WritesIntoProcessedFolder_PreservingRelativePath()
    {
        var root = Path.Combine(Path.GetTempPath(), "asm-root-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "Book1", "chapter.mp3");
        var output = Path.Combine(root, "Processed");

        var decision = _resolver.Resolve(source, root, output, OutputConflictPolicy.Skip);

        Assert.Equal(OutputPathKind.Write, decision.Kind);
        Assert.Equal(Path.Combine(output, "Book1", "chapter.mp3"), decision.OutputPath);
    }

    [Fact]
    public void ExistingOutput_IsSkipped()
    {
        var folder = Path.Combine(Path.GetTempPath(), "asm-out-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var source = Path.Combine(folder, "src", "chapter.mp3");
        var output = Path.Combine(folder, "Processed", "chapter.mp3");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, "existing");

        var decision = _resolver.Resolve(source, Path.Combine(folder, "src"), Path.Combine(folder, "Processed"), OutputConflictPolicy.Skip);

        Assert.Equal(OutputPathKind.SkipExisting, decision.Kind);
        Directory.Delete(folder, true);
    }

    [Fact]
    public void SamePathAsSource_IsRefused()
    {
        var folder = Path.Combine(Path.GetTempPath(), "asm-same-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(folder, "chapter.mp3");

        var decision = _resolver.Resolve(source, folder, folder, OutputConflictPolicy.Skip);

        Assert.Equal(OutputPathKind.RefuseOverwriteOriginal, decision.Kind);
    }
}
