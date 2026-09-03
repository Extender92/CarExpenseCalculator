using System.Diagnostics;

namespace CarExpenseCalculator.CodexExtractor;

internal interface IOwnedProcessFactory
{
    IOwnedProcess Start(ProcessStartInfo startInfo);
}

internal interface IOwnedProcess : IDisposable
{
    StreamWriter StandardInput { get; }

    Stream StandardOutput { get; }

    StreamReader StandardError { get; }

    int ExitCode { get; }

    Task WaitForExitAsync(CancellationToken cancellationToken);

    void KillTree();
}

internal sealed class SystemOwnedProcessFactory : IOwnedProcessFactory
{
    public IOwnedProcess Start(ProcessStartInfo startInfo)
    {
        var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
            return new SystemOwnedProcess(process);
        }
        catch
        {
            process.Dispose();
            throw;
        }
    }

    private sealed class SystemOwnedProcess(Process process) : IOwnedProcess
    {
        public StreamWriter StandardInput => process.StandardInput;

        public Stream StandardOutput => process.StandardOutput.BaseStream;

        public StreamReader StandardError => process.StandardError;

        public int ExitCode => process.ExitCode;

        public Task WaitForExitAsync(CancellationToken cancellationToken) =>
            process.WaitForExitAsync(cancellationToken);

        public void KillTree()
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
            }
        }

        public void Dispose() => process.Dispose();
    }
}
