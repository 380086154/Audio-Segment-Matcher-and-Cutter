using AudioMatcher.Core.Interfaces;
using AudioMatcher.Core.Models;
using AudioMatcher.Core.Services;
using AudioMatcher.Tests.Support;

namespace AudioMatcher.Tests.Processing;

public sealed class AudioProcessingServiceTests
{
    [Fact]
    public async Task FailedFile_DoesNotStopBatch()
    {
        var processor = new ScriptedProcessor();
        var logger = new RecordingLogger();
        var service = new AudioProcessingService(processor, logger);
        var requests = new[]
        {
            Request("a.mp3"),
            Request("b.mp3"),
            Request("c.mp3")
        };

        var result = await service.ProcessAsync(requests);

        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(3, logger.Results.Count);
    }

    [Fact]
    public async Task OriginalFile_IsNotModified()
    {
        var folder = Path.Combine(Path.GetTempPath(), "asm-safe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var source = Path.Combine(folder, "chapter.mp3");
        var original = new byte[] { 1, 2, 3, 4, 5 };
        await File.WriteAllBytesAsync(source, original);

        var processor = new CopyToOutputProcessor();
        var service = new AudioProcessingService(processor, new RecordingLogger());
        var request = new ProcessingRequest
        {
            SourceFilePath = source,
            SearchRoot = folder,
            OutputFolder = Path.Combine(folder, "Processed"),
            Action = ProcessingAction.RemoveAllMatches,
            FileDuration = TimeSpan.FromMinutes(1),
            Matches = [TestAudio.Match(source, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2))]
        };

        var result = await service.ProcessAsync([request]);

        Assert.Equal(ProcessingFileStatus.Success, result.Files[0].Status);
        Assert.Equal(original, await File.ReadAllBytesAsync(source));
        Assert.True(File.Exists(result.Files[0].OutputFilePath));
        Assert.False(string.Equals(source, result.Files[0].OutputFilePath, StringComparison.OrdinalIgnoreCase));
        Directory.Delete(folder, true);
    }

    [Fact]
    public async Task ProcessesFilesInParallel()
    {
        var service = new AudioProcessingService(new BarrierProcessor(), new RecordingLogger());
        var requests = Enumerable.Range(0, 8).Select(i => Request($"f{i}.mp3")).ToArray();

        var result = await service.ProcessAsync(requests);

        Assert.Equal(8, result.SuccessCount);
        Assert.Equal(0, result.FailedCount);
    }

    private static ProcessingRequest Request(string name) => new()
    {
        SourceFilePath = name,
        SearchRoot = "in",
        OutputFolder = "out",
        Action = ProcessingAction.RemoveAllMatches,
        FileDuration = TimeSpan.FromMinutes(1),
        Matches = [TestAudio.Match(name, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2))]
    };

    private sealed class ScriptedProcessor : IAudioProcessor
    {
        public Task<ProcessingFileResult> ProcessAsync(ProcessingRequest request, CancellationToken cancellationToken = default)
        {
            if (request.SourceFilePath.Contains("b.mp3", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(ProcessingFileResult.Fail(request.SourceFilePath, "boom"));
            }

            return Task.FromResult(ProcessingFileResult.Success(request, "out\\" + request.SourceFilePath));
        }
    }

    private sealed class CopyToOutputProcessor : IAudioProcessor
    {
        public Task<ProcessingFileResult> ProcessAsync(ProcessingRequest request, CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(request.OutputFolder);
            var output = Path.Combine(request.OutputFolder, Path.GetFileName(request.SourceFilePath));
            File.Copy(request.SourceFilePath, output);
            return Task.FromResult(ProcessingFileResult.Success(request, output));
        }
    }

    private sealed class RecordingLogger : IProcessingLogger
    {
        public string DirectoryPath { get; } = Path.GetTempPath();

        public List<ProcessingFileResult> Results { get; } = [];

        public void Log(ProcessingFileResult result)
        {
            lock (Results)
            {
                Results.Add(result);
            }
        }
    }

    private sealed class BarrierProcessor : IAudioProcessor
    {
        private readonly TaskCompletionSource _started = new();
        private int _inFlight;

        public async Task<ProcessingFileResult> ProcessAsync(ProcessingRequest request, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _inFlight) == 8)
            {
                _started.TrySetResult();
            }

            await _started.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            return ProcessingFileResult.Success(request, "out\\" + request.SourceFilePath);
        }
    }
}
