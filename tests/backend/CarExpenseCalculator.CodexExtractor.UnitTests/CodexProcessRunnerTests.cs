namespace CarExpenseCalculator.CodexExtractor.UnitTests;

public sealed class CodexProcessRunnerTests
{
    [Fact]
    public void Invocation_is_owned_isolated_and_does_not_put_the_url_in_arguments()
    {
        var options = TestData.CreateOptions();
        var runner = new CodexProcessRunner(options);
        var arguments = runner.BuildArguments("example.com", "/tmp/owned-work");

        Assert.Equal("exec", arguments[0]);
        Assert.Contains("--strict-config", arguments);
        Assert.Contains("gpt-5.6-luna", arguments);
        Assert.Contains("medium", string.Join(' ', arguments));
        Assert.Contains("--ephemeral", arguments);
        Assert.Contains("--json", arguments);
        Assert.Contains("--output-schema", arguments);
        Assert.Contains("read-only", arguments);
        Assert.Contains("--skip-git-repo-check", arguments);
        Assert.Contains("--ignore-user-config", arguments);
        Assert.Contains("--ignore-rules", arguments);
        Assert.Contains("approval_policy=\"never\"", arguments);
        Assert.Contains("web_search=\"live\"", arguments);
        Assert.Contains("tools.web_search={context_size=\"medium\",allowed_domains=[\"example.com\"]}", arguments);
        Assert.Contains("agents.enabled=false", arguments);
        Assert.Contains("apps._default.enabled=false", arguments);
        Assert.Contains("features.shell_tool=false", arguments);
        Assert.Contains("features.skill_mcp_dependency_install=false", arguments);
        Assert.Contains("tools.view_image=false", arguments);
        Assert.Contains("allow_login_shell=false", arguments);
        Assert.Contains("forced_login_method=\"chatgpt\"", arguments);
        Assert.Contains("cli_auth_credentials_store=\"file\"", arguments);
        Assert.Equal("-", arguments[^1]);
        Assert.DoesNotContain("https://example.com/item/secret", string.Join(' ', arguments));
    }

    [Fact]
    public void Child_environment_is_allowlisted_and_contains_no_api_or_database_secret()
    {
        var runner = new CodexProcessRunner(TestData.CreateOptions("C:\\dedicated-codex-home"));
        var startInfo = runner.CreateStartInfo(["--version"]);

        Assert.Equal("C:\\dedicated-codex-home", startInfo.Environment["CODEX_HOME"]);
        Assert.False(startInfo.Environment.ContainsKey("OPENAI_API_KEY"));
        Assert.False(startInfo.Environment.ContainsKey("CODEX_API_KEY"));
        Assert.False(startInfo.Environment.ContainsKey("ConnectionStrings__Postgres"));
        Assert.DoesNotContain(
            startInfo.Environment.Keys,
            key => key.StartsWith("POSTGRES_", StringComparison.Ordinal));
        Assert.All(
            startInfo.Environment.Keys,
            key => Assert.Contains(
                key,
                new[] { "PATH", "HOME", "SSL_CERT_FILE", "SSL_CERT_DIR", "CODEX_HOME", "LANG", "LC_ALL" }));
    }

    [Fact]
    public async Task Jsonl_reader_bounds_retained_lines_but_drains_the_stream()
    {
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("kept\ntoo-long\nafter\n"));

        var output = await CodexProcessRunner.ReadLinesAsync(
            stream,
            maximumLineBytes: 5,
            maximumTotalBytes: 100,
            CancellationToken.None);

        Assert.True(output.LimitExceeded);
        Assert.Equal(["kept"], output.Lines);
        Assert.Equal(stream.Length, stream.Position);
    }

    [Fact]
    public async Task Stderr_reader_bounds_retained_text_but_drains_the_stream()
    {
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("sensitive-output"));
        using var reader = new StreamReader(stream);

        var output = await CodexProcessRunner.ReadBoundedTextAsync(
            reader,
            maximumBytes: 4,
            CancellationToken.None);

        Assert.True(output.LimitExceeded);
        Assert.Equal(string.Empty, output.Text);
        Assert.Equal(stream.Length, stream.Position);
    }

    [Fact]
    public async Task Jsonl_reader_rejects_invalid_utf8_without_retaining_it()
    {
        await using var stream = new MemoryStream([0xff, (byte)'\n']);

        var output = await CodexProcessRunner.ReadLinesAsync(
            stream,
            maximumLineBytes: 100,
            maximumTotalBytes: 100,
            CancellationToken.None);

        Assert.True(output.LimitExceeded);
        Assert.Empty(output.Lines);
        Assert.Equal(stream.Length, stream.Position);
    }

    [Fact]
    public async Task Cancellation_kills_the_owned_process_tree_and_removes_the_request_directory()
    {
        var workRoot = Path.Combine(Path.GetTempPath(), "car-expense-process-test", Guid.NewGuid().ToString("N"));
        var options = TestData.CreateOptions() with { WorkRoot = workRoot };
        var process = new BlockingOwnedProcess();
        var runner = new CodexProcessRunner(options, new FakeOwnedProcessFactory(process));
        using var cancellation = new CancellationTokenSource();

        var execution = runner.RunAsync("example.com", "private prompt", cancellation.Token);
        await process.WaitStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
        Assert.True(process.ProcessTreeKilled);
        Assert.Equal("private prompt", process.StandardInputText);
        Assert.Empty(Directory.EnumerateDirectories(workRoot));
    }

    private sealed class FakeOwnedProcessFactory(BlockingOwnedProcess process) : IOwnedProcessFactory
    {
        public IOwnedProcess Start(System.Diagnostics.ProcessStartInfo startInfo) => process;
    }

    private sealed class BlockingOwnedProcess : IOwnedProcess
    {
        private readonly MemoryStream standardInput = new();
        private readonly MemoryStream standardOutput = new();
        private readonly MemoryStream standardError = new();

        public BlockingOwnedProcess()
        {
            StandardInput = new StreamWriter(
                standardInput,
                new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                bufferSize: 1024,
                leaveOpen: true)
            {
                AutoFlush = true,
            };
            StandardError = new StreamReader(
                standardError,
                System.Text.Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 1024,
                leaveOpen: true);
        }

        public TaskCompletionSource WaitStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public StreamWriter StandardInput { get; }

        public Stream StandardOutput => standardOutput;

        public StreamReader StandardError { get; }

        public int ExitCode => 0;

        public bool ProcessTreeKilled { get; private set; }

        public string StandardInputText => System.Text.Encoding.UTF8.GetString(standardInput.ToArray());

        public async Task WaitForExitAsync(CancellationToken cancellationToken)
        {
            WaitStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        public void KillTree() => ProcessTreeKilled = true;

        public void Dispose()
        {
            StandardInput.Dispose();
            StandardError.Dispose();
            standardOutput.Dispose();
            standardError.Dispose();
        }
    }
}
