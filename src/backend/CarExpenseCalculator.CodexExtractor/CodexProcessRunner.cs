using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace CarExpenseCalculator.CodexExtractor;

internal sealed class CodexProcessRunner : ICodexProcessRunner
{
    private const int MaximumJsonlLineBytes = 1 * 1024 * 1024;
    private const int MaximumStandardOutputBytes = 10 * 1024 * 1024;
    private const int MaximumStandardErrorBytes = 1 * 1024 * 1024;
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);
    private readonly CodexExtractorOptions options;
    private readonly IOwnedProcessFactory processFactory;

    public CodexProcessRunner(CodexExtractorOptions options)
        : this(options, new SystemOwnedProcessFactory())
    {
    }

    internal CodexProcessRunner(
        CodexExtractorOptions options,
        IOwnedProcessFactory processFactory)
    {
        this.options = options;
        this.processFactory = processFactory;
    }

    public async Task<CodexInstallationStatus> GetInstallationStatusAsync(
        CancellationToken cancellationToken)
    {
        var version = await RunProbeAsync(["--version"], cancellationToken);
        var hasRequiredVersion = version.ExitCode == 0
            && version.Output.Trim().Equals(
                $"codex-cli {CodexExtractorOptions.RequiredCliVersion}",
                StringComparison.Ordinal);
        if (!hasRequiredVersion)
        {
            return new CodexInstallationStatus(false, false);
        }

        var login = await RunProbeAsync(
            [
                "login",
                "status",
                "-c",
                "forced_login_method=\"chatgpt\"",
                "-c",
                "cli_auth_credentials_store=\"file\"",
            ],
            cancellationToken);
        var hasChatGptAuthentication = login.ExitCode == 0
            && login.Output.Contains("ChatGPT", StringComparison.OrdinalIgnoreCase);

        return new CodexInstallationStatus(true, hasChatGptAuthentication);
    }

    public async Task<CodexProcessResult> RunAsync(
        string host,
        string prompt,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        Directory.CreateDirectory(options.WorkRoot);
        var workDirectory = Path.Combine(options.WorkRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDirectory);

        try
        {
            using var process = processFactory.Start(
                CreateStartInfo(BuildArguments(host, workDirectory)));

            using var cancellationRegistration = cancellationToken.Register(
                static state => ((IOwnedProcess)state!).KillTree(),
                process);

            var standardOutputTask = ReadLinesAsync(
                process.StandardOutput,
                MaximumJsonlLineBytes,
                MaximumStandardOutputBytes,
                cancellationToken);
            var standardErrorTask = ReadBoundedTextAsync(
                process.StandardError,
                MaximumStandardErrorBytes,
                cancellationToken);

            try
            {
                await process.StandardInput.WriteAsync(prompt.AsMemory(), cancellationToken);
                await process.StandardInput.FlushAsync(cancellationToken);
                process.StandardInput.Close();
                await process.WaitForExitAsync(cancellationToken);
                var standardOutput = await standardOutputTask;
                var standardError = await standardErrorTask;
                return new CodexProcessResult(
                    process.ExitCode,
                    standardOutput.Lines,
                    standardError.Text,
                    standardOutput.LimitExceeded || standardError.LimitExceeded);
            }
            catch (Exception exception) when (exception is OperationCanceledException or IOException)
            {
                process.KillTree();
                await DrainAfterTerminationAsync(standardOutputTask, standardErrorTask);
                throw;
            }
        }
        finally
        {
            DeleteOwnedWorkDirectory(workDirectory);
        }
    }

    internal IReadOnlyList<string> BuildArguments(string host, string workDirectory)
    {
        var quotedHost = JsonSerializer.Serialize(host);
        return
        [
            "exec",
            "--strict-config",
            "--model",
            options.Model,
            "--sandbox",
            "read-only",
            "--ephemeral",
            "--json",
            "--output-schema",
            options.SchemaPath,
            "--skip-git-repo-check",
            "--ignore-user-config",
            "--ignore-rules",
            "--color",
            "never",
            "--cd",
            workDirectory,
            "-c",
            $"model_reasoning_effort=\"{options.ReasoningEffort}\"",
            "-c",
            "approval_policy=\"never\"",
            "-c",
            "web_search=\"live\"",
            "-c",
            $"tools.web_search={{context_size=\"medium\",allowed_domains=[{quotedHost}]}}",
            "-c",
            "forced_login_method=\"chatgpt\"",
            "-c",
            "cli_auth_credentials_store=\"file\"",
            "-c",
            "agents.enabled=false",
            "-c",
            "apps._default.enabled=false",
            "-c",
            "features.shell_tool=false",
            "-c",
            "features.skill_mcp_dependency_install=false",
            "-c",
            "tools.view_image=false",
            "-c",
            "allow_login_shell=false",
            "-c",
            "check_for_update_on_startup=false",
            "-",
        ];
    }

    internal ProcessStartInfo CreateStartInfo(IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = options.CodexExecutable,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardInputEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        ApplySafeEnvironment(startInfo);
        return startInfo;
    }

    private async Task<ProbeResult> RunProbeAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ProbeTimeout);
        try
        {
            using var process = processFactory.Start(CreateStartInfo(arguments));
            process.StandardInput.Close();
            using var cancellationRegistration = timeout.Token.Register(
                static state => ((IOwnedProcess)state!).KillTree(),
                process);
            using var outputReader = new StreamReader(
                process.StandardOutput,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 1024,
                leaveOpen: true);
            var outputTask = ReadBoundedTextAsync(
                outputReader,
                MaximumStandardErrorBytes,
                timeout.Token);
            var errorTask = ReadBoundedTextAsync(
                process.StandardError,
                MaximumStandardErrorBytes,
                timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            var output = await outputTask;
            _ = await errorTask;
            return new ProbeResult(process.ExitCode, output.Text);
        }
        catch (Exception exception) when (
            exception is OperationCanceledException
            or InvalidOperationException
            or System.ComponentModel.Win32Exception)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            return new ProbeResult(-1, string.Empty);
        }
    }

    private void ApplySafeEnvironment(ProcessStartInfo startInfo)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        var home = Environment.GetEnvironmentVariable("HOME");
        var sslCertificateFile = Environment.GetEnvironmentVariable("SSL_CERT_FILE");
        var sslCertificateDirectory = Environment.GetEnvironmentVariable("SSL_CERT_DIR");

        startInfo.Environment.Clear();
        CopyIfPresent(startInfo, "PATH", path);
        CopyIfPresent(startInfo, "HOME", home);
        CopyIfPresent(startInfo, "SSL_CERT_FILE", sslCertificateFile);
        CopyIfPresent(startInfo, "SSL_CERT_DIR", sslCertificateDirectory);
        startInfo.Environment["CODEX_HOME"] = options.CodexHome!;
        startInfo.Environment["LANG"] = "C.UTF-8";
        startInfo.Environment["LC_ALL"] = "C.UTF-8";
    }

    private static void CopyIfPresent(ProcessStartInfo startInfo, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            startInfo.Environment[name] = value;
        }
    }

    internal static async Task<BoundedLines> ReadLinesAsync(
        Stream stream,
        int maximumLineBytes,
        int maximumTotalBytes,
        CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        var buffer = new byte[8192];
        var currentLine = new List<byte>(Math.Min(maximumLineBytes, buffer.Length));
        long totalBytes = 0;
        var limitExceeded = false;
        while (true)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (count == 0)
            {
                break;
            }

            for (var index = 0; index < count; index++)
            {
                var value = buffer[index];
                totalBytes++;
                if (totalBytes > maximumTotalBytes)
                {
                    limitExceeded = true;
                    currentLine.Clear();
                    continue;
                }

                if (value == (byte)'\n')
                {
                    if (!limitExceeded)
                    {
                        limitExceeded = !TryAddDecodedLine(lines, currentLine);
                    }

                    currentLine.Clear();
                    continue;
                }

                if (limitExceeded)
                {
                    continue;
                }

                if (currentLine.Count >= maximumLineBytes)
                {
                    limitExceeded = true;
                    currentLine.Clear();
                    continue;
                }

                currentLine.Add(value);
            }
        }

        if (!limitExceeded && currentLine.Count > 0)
        {
            limitExceeded = !TryAddDecodedLine(lines, currentLine);
        }

        return new BoundedLines(Array.AsReadOnly(lines.ToArray()), limitExceeded);
    }

    private static bool TryAddDecodedLine(ICollection<string> lines, List<byte> bytes)
    {
        var count = bytes.Count > 0 && bytes[^1] == (byte)'\r'
            ? bytes.Count - 1
            : bytes.Count;
        try
        {
            lines.Add(StrictUtf8.GetString(CollectionsMarshal.AsSpan(bytes)[..count]));
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    internal static async Task<BoundedText> ReadBoundedTextAsync(
        StreamReader reader,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        var buffer = new char[4096];
        var builder = new StringBuilder();
        long totalBytes = 0;
        var limitExceeded = false;
        while (true)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (count == 0)
            {
                break;
            }

            totalBytes += Encoding.UTF8.GetByteCount(buffer.AsSpan(0, count));
            if (limitExceeded || totalBytes > maximumBytes)
            {
                limitExceeded = true;
                continue;
            }

            builder.Append(buffer, 0, count);
        }

        return new BoundedText(builder.ToString(), limitExceeded);
    }

    private static async Task DrainAfterTerminationAsync(
        Task<BoundedLines> standardOutputTask,
        Task<BoundedText> standardErrorTask)
    {
        try
        {
            await Task.WhenAll(standardOutputTask, standardErrorTask);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void DeleteOwnedWorkDirectory(string workDirectory)
    {
        var root = Path.GetFullPath(options.WorkRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(workDirectory);
        if (candidate.StartsWith(root, StringComparison.Ordinal)
            && Directory.Exists(candidate))
        {
            Directory.Delete(candidate, recursive: true);
        }
    }

    private sealed record ProbeResult(int ExitCode, string Output);

    internal sealed record BoundedLines(IReadOnlyList<string> Lines, bool LimitExceeded);

    internal sealed record BoundedText(string Text, bool LimitExceeded);
}
