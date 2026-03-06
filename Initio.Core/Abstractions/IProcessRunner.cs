namespace Initio.Core.Abstractions;

public interface IProcessRunner
{
    Task<ProcessResult?> RunAsync(ProcessSpec spec, TimeSpan timeout, CancellationToken cancellationToken = default);
}

public sealed record ProcessSpec(
    string FileName,
    string Arguments,
    bool UseShellExecute = false,
    bool RedirectStandardOutput = true,
    bool RedirectStandardError = true,
    bool CreateNoWindow = true);

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    public string CombinedOutput => string.IsNullOrWhiteSpace(StandardOutput) ? StandardError : StandardOutput;
}
