using System.Diagnostics;
using Initio.Core.Abstractions;

namespace NewPCSetupWPF.Services;

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult?> RunAsync(ProcessSpec spec, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = spec.FileName,
            Arguments = spec.Arguments,
            UseShellExecute = spec.UseShellExecute,
            RedirectStandardOutput = spec.RedirectStandardOutput,
            RedirectStandardError = spec.RedirectStandardError,
            CreateNoWindow = spec.CreateNoWindow
        };

        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var outputTask = spec.RedirectStandardOutput ? process.StandardOutput.ReadToEndAsync() : Task.FromResult(string.Empty);
            var errorTask = spec.RedirectStandardError ? process.StandardError.ReadToEndAsync() : Task.FromResult(string.Empty);

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(timeout);

            try
            {
                await process.WaitForExitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                TryKill(process);
                return null;
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                throw;
            }

            var output = await outputTask;
            var error = await errorTask;
            return new ProcessResult(process.ExitCode, output, error);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
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
        }
    }
}
