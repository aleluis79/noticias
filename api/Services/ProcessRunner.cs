using System.Diagnostics;

namespace NewsAggregator.Services
{
    public sealed record ProcessResult(int ExitCode, string StandardError);

    /// <summary>Runs an external process so the speech engine can be tested without it.</summary>
    public interface IProcessRunner
    {
        Task<ProcessResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            string? standardInput,
            CancellationToken cancellationToken);
    }

    public sealed class ProcessRunner : IProcessRunner
    {
        public async Task<ProcessResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            string? standardInput,
            CancellationToken cancellationToken)
        {
            var startInfo = new ProcessStartInfo(fileName)
            {
                RedirectStandardInput = standardInput is not null,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"No se pudo iniciar {fileName}.");

            if (standardInput is not null)
            {
                await process.StandardInput.WriteAsync(standardInput);
                process.StandardInput.Close();
            }

            // Drain both pipes before waiting to avoid a full buffer deadlock.
            var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            await stdout;
            var error = await stderr;

            return new ProcessResult(process.ExitCode, error);
        }
    }
}
