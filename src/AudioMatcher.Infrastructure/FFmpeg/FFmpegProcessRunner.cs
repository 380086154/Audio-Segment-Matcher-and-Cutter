using System.Diagnostics;
using System.Text;

namespace AudioMatcher.Infrastructure.FFmpeg;

public interface IFFmpegProcessRunner
{
    Task<byte[]> RunToBytesAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default);

    Task<string> RunToTextAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default);

    Task RunToFileAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default);

    Task RunStreamingAsync(
        string executable,
        IReadOnlyList<string> arguments,
        Func<byte[], int, CancellationToken, ValueTask> onData,
        CancellationToken cancellationToken = default);

    Task<string> CaptureStdErrAsync(
        string executable,
        IReadOnlyList<string> arguments,
        bool throwOnError = true,
        CancellationToken cancellationToken = default);
}

public sealed class FFmpegProcessRunner : IFFmpegProcessRunner
{
    public async Task<byte[]> RunToBytesAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await RunStreamingAsync(executable, arguments, (data, count, _) =>
        {
            buffer.Write(data, 0, count);
            return ValueTask.CompletedTask;
        }, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    public async Task<string> RunToTextAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        var bytes = await RunToBytesAsync(executable, arguments, cancellationToken).ConfigureAwait(false);
        return Encoding.UTF8.GetString(bytes);
    }

    public Task RunToFileAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
        => RunCoreAsync(executable, arguments, redirectStdout: false, null, throwOnError: true, cancellationToken);

    public Task RunStreamingAsync(
        string executable,
        IReadOnlyList<string> arguments,
        Func<byte[], int, CancellationToken, ValueTask> onData,
        CancellationToken cancellationToken = default)
        => RunCoreAsync(executable, arguments, redirectStdout: true, onData, throwOnError: true, cancellationToken);

    public Task<string> CaptureStdErrAsync(
        string executable,
        IReadOnlyList<string> arguments,
        bool throwOnError = true,
        CancellationToken cancellationToken = default)
        => RunCoreAsync(executable, arguments, redirectStdout: false, null, throwOnError, cancellationToken);

    private static async Task<string> RunCoreAsync(
        string executable,
        IReadOnlyList<string> arguments,
        bool redirectStdout,
        Func<byte[], int, CancellationToken, ValueTask>? onData,
        bool throwOnError,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = redirectStdout
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start {executable}.");
        }

        await using var registration = cancellationToken.Register(static state =>
        {
            try
            {
                ((Process)state!).Kill(entireProcessTree: true);
            }
            catch
            {
                // The process may have already exited.
            }
        }, process);

        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        Task stdoutTask = Task.CompletedTask;
        if (redirectStdout && onData is not null)
        {
            stdoutTask = ReadStdoutAsync(process, onData, cancellationToken);
        }

        try
        {
            await Task.WhenAll(process.WaitForExitAsync(cancellationToken), stdoutTask).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        var stderr = await stderrTask.ConfigureAwait(false);
        if (throwOnError && process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{Path.GetFileName(executable)} failed with exit code {process.ExitCode}. {stderr}".Trim());
        }

        return stderr;
    }

    private static async Task ReadStdoutAsync(
        Process process,
        Func<byte[], int, CancellationToken, ValueTask> onData,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        var stream = process.StandardOutput.BaseStream;
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
            if (read <= 0)
            {
                break;
            }

            await onData(buffer, read, cancellationToken).ConfigureAwait(false);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Ignore cleanup failures.
        }
    }
}
