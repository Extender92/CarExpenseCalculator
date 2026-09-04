using CarExpenseCalculator.Extraction.Contracts;
using Microsoft.Extensions.Logging;

namespace CarExpenseCalculator.CodexExtractor.UnitTests;

public sealed class CodexExtractionOrchestratorTests
{
    private static readonly ListingExtractionRequest ValidRequest = new(
        "https://example.com/item/1",
        ListingExtractionContractVersions.Prompt,
        ListingExtractionContractVersions.Schema);

    [Fact]
    public async Task Successful_execution_returns_requested_model_and_structured_output()
    {
        var runner = FakeRunner.Success();
        var logger = new RecordingLogger<CodexExtractionOrchestrator>();
        var orchestrator = CreateOrchestrator(runner, logger: logger);

        var execution = await orchestrator.ExecuteAsync(ValidRequest, CancellationToken.None);

        var success = Assert.IsType<CodexExtractionSucceeded>(execution);
        Assert.Equal("gpt-5.6-luna", success.Response.RequestedModel);
        Assert.Single(success.Response.Sources);
        Assert.Equal(1, runner.RunCalls);
        Assert.DoesNotContain("https://example.com/item/1", string.Join(' ', logger.Messages));
        Assert.DoesNotContain(TestData.EmptyDraftJson(), string.Join(' ', logger.Messages));
    }

    [Theory]
    [InlineData(1, 2, (int)CodexExecutionFailure.UnsupportedVersion)]
    [InlineData(2, 1, (int)CodexExecutionFailure.UnsupportedVersion)]
    [InlineData(2, 99, (int)CodexExecutionFailure.UnsupportedVersion)]
    [InlineData(99, 2, (int)CodexExecutionFailure.UnsupportedVersion)]
    public async Task Unsupported_versions_never_start_codex(
        int promptVersion,
        int schemaVersion,
        int expectedValue)
    {
        var runner = FakeRunner.Success();
        var request = new ListingExtractionRequest(
            ValidRequest.NormalizedUrl,
            promptVersion,
            schemaVersion);

        var execution = await CreateOrchestrator(runner)
            .ExecuteAsync(request, CancellationToken.None);

        Assert.Equal(
            (CodexExecutionFailure)expectedValue,
            Assert.IsType<CodexExtractionFailed>(execution).Failure);
        Assert.Equal(0, runner.RunCalls);
    }

    [Fact]
    public async Task Missing_authentication_is_not_configured_and_starts_no_turn()
    {
        var runner = FakeRunner.Success();
        runner.InstallationStatus = new CodexInstallationStatus(true, false);

        var execution = await CreateOrchestrator(runner)
            .ExecuteAsync(ValidRequest, CancellationToken.None);

        Assert.Equal(
            CodexExecutionFailure.NotConfigured,
            Assert.IsType<CodexExtractionFailed>(execution).Failure);
        Assert.Equal(0, runner.RunCalls);
    }

    [Theory]
    [InlineData("https://example.com/item/1#fragment")]
    [InlineData("http://127.0.0.1/private")]
    [InlineData("")]
    public async Task Invalid_or_non_normalized_urls_never_start_codex(string value)
    {
        var runner = FakeRunner.Success();
        var request = ValidRequest with { NormalizedUrl = value };

        var execution = await CreateOrchestrator(runner)
            .ExecuteAsync(request, CancellationToken.None);

        Assert.Equal(
            CodexExecutionFailure.InvalidRequest,
            Assert.IsType<CodexExtractionFailed>(execution).Failure);
        Assert.Equal(0, runner.RunCalls);
    }

    [Fact]
    public async Task Invalid_owned_configuration_is_not_configured()
    {
        var runner = FakeRunner.Success();
        var options = TestData.CreateOptions() with { ReasoningEffort = "low" };

        var execution = await CreateOrchestrator(runner, options)
            .ExecuteAsync(ValidRequest, CancellationToken.None);

        Assert.Equal(
            CodexExecutionFailure.NotConfigured,
            Assert.IsType<CodexExtractionFailed>(execution).Failure);
        Assert.Equal(0, runner.RunCalls);
    }

    [Theory]
    [InlineData("HTTP 429", (int)CodexExecutionFailure.RateLimited)]
    [InlineData("usage limit reached", (int)CodexExecutionFailure.RateLimited)]
    [InlineData("runtime unavailable", (int)CodexExecutionFailure.ProviderUnavailable)]
    public async Task Nonzero_exit_is_typed_without_retry(
        string standardError,
        int expectedValue)
    {
        var runner = FakeRunner.Success();
        runner.Result = new CodexProcessResult(1, [], standardError, false);

        var execution = await CreateOrchestrator(runner)
            .ExecuteAsync(ValidRequest, CancellationToken.None);

        Assert.Equal(
            (CodexExecutionFailure)expectedValue,
            Assert.IsType<CodexExtractionFailed>(execution).Failure);
        Assert.Equal(1, runner.RunCalls);
    }

    [Fact]
    public async Task Excessive_output_is_invalid_and_is_not_retried()
    {
        var runner = FakeRunner.Success();
        runner.Result = runner.Result with { OutputLimitExceeded = true };

        var execution = await CreateOrchestrator(runner)
            .ExecuteAsync(ValidRequest, CancellationToken.None);

        Assert.Equal(
            CodexExecutionFailure.InvalidOutput,
            Assert.IsType<CodexExtractionFailed>(execution).Failure);
        Assert.Equal(1, runner.RunCalls);
    }

    [Fact]
    public async Task Failure_logs_do_not_contain_stderr_url_output_or_credentials()
    {
        const string sensitive = "https://example.com/item/1 fake-access-token extracted Volvo";
        var runner = FakeRunner.Success();
        runner.Result = new CodexProcessResult(1, [sensitive], $"HTTP 429 {sensitive}", false);
        var logger = new RecordingLogger<CodexExtractionOrchestrator>();

        var execution = await CreateOrchestrator(runner, logger: logger)
            .ExecuteAsync(ValidRequest, CancellationToken.None);

        Assert.Equal(
            CodexExecutionFailure.RateLimited,
            Assert.IsType<CodexExtractionFailed>(execution).Failure);
        Assert.DoesNotContain(sensitive, string.Join(' ', logger.Messages), StringComparison.Ordinal);
        Assert.DoesNotContain("fake-access-token", string.Join(' ', logger.Messages), StringComparison.Ordinal);
        Assert.DoesNotContain("Volvo", string.Join(' ', logger.Messages), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Timeout_is_typed_and_releases_capacity()
    {
        var runner = FakeRunner.Blocking();
        var options = TestData.CreateOptions() with { OperationTimeout = TimeSpan.FromMilliseconds(30) };
        var orchestrator = CreateOrchestrator(runner, options);

        var first = await orchestrator.ExecuteAsync(ValidRequest, CancellationToken.None);
        var second = await orchestrator.ExecuteAsync(ValidRequest, CancellationToken.None);

        Assert.Equal(CodexExecutionFailure.TimedOut, Assert.IsType<CodexExtractionFailed>(first).Failure);
        Assert.Equal(CodexExecutionFailure.TimedOut, Assert.IsType<CodexExtractionFailed>(second).Failure);
        Assert.Equal(2, runner.RunCalls);
    }

    [Fact]
    public async Task Queue_time_counts_toward_the_operation_timeout()
    {
        var runner = FakeRunner.Success();
        var options = TestData.CreateOptions() with { OperationTimeout = TimeSpan.FromMilliseconds(30) };
        using var heldGate = new SemaphoreSlim(initialCount: 0, maxCount: 2);
        var orchestrator = CreateOrchestrator(runner, options, concurrencyGate: heldGate);

        var execution = await orchestrator.ExecuteAsync(ValidRequest, CancellationToken.None);

        Assert.Equal(
            CodexExecutionFailure.TimedOut,
            Assert.IsType<CodexExtractionFailed>(execution).Failure);
        Assert.Equal(0, runner.RunCalls);
        Assert.Equal(0, heldGate.CurrentCount);
    }

    [Fact]
    public async Task Caller_cancellation_propagates_and_never_retries()
    {
        var runner = FakeRunner.Blocking();
        var orchestrator = CreateOrchestrator(runner);
        using var cancellation = new CancellationTokenSource();

        var execution = orchestrator.ExecuteAsync(ValidRequest, cancellation.Token);
        await runner.FirstStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);

        Assert.Equal(1, runner.RunCalls);
    }

    [Fact]
    public async Task Process_wide_concurrency_never_exceeds_two()
    {
        var runner = FakeRunner.Blocking();
        var orchestrator = CreateOrchestrator(runner);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var tasks = Enumerable.Range(0, 3)
            .Select(_ => orchestrator.ExecuteAsync(ValidRequest, cancellation.Token))
            .ToArray();
        await runner.TwoStarted.Task.WaitAsync(cancellation.Token);

        Assert.Equal(2, runner.MaximumConcurrentRuns);
        Assert.Equal(2, runner.RunCalls);

        runner.Release.TrySetResult();
        await Task.WhenAll(tasks);
        Assert.Equal(2, runner.MaximumConcurrentRuns);
        Assert.Equal(3, runner.RunCalls);
    }

    private static CodexExtractionOrchestrator CreateOrchestrator(
        FakeRunner runner,
        CodexExtractorOptions? options = null,
        ILogger<CodexExtractionOrchestrator>? logger = null,
        SemaphoreSlim? concurrencyGate = null)
    {
        options ??= TestData.CreateOptions();
        var parser = new CodexJsonlParser(new ExtractionOutputValidator(options));
        var resolvedLogger = logger ?? new RecordingLogger<CodexExtractionOrchestrator>();
        return concurrencyGate is null
            ? new CodexExtractionOrchestrator(
                options,
                runner,
                parser,
                TimeProvider.System,
                resolvedLogger)
            : new CodexExtractionOrchestrator(
                options,
                runner,
                parser,
                TimeProvider.System,
                resolvedLogger,
                concurrencyGate);
    }

    private sealed class FakeRunner : ICodexProcessRunner
    {
        private readonly bool block;
        private int concurrentRuns;

        private FakeRunner(bool block)
        {
            this.block = block;
            Result = new CodexProcessResult(
                0,
                TestData.SuccessfulJsonl(
                    TestData.EmptyDraftJson(),
                    TestData.WebEvent("open_page", ValidRequest.NormalizedUrl)),
                string.Empty,
                false);
        }

        public CodexInstallationStatus InstallationStatus { get; set; } = new(true, true);

        public CodexProcessResult Result { get; set; }

        public int RunCalls { get; private set; }

        public int MaximumConcurrentRuns { get; private set; }

        public TaskCompletionSource TwoStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource FirstStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public static FakeRunner Success() => new(block: false);

        public static FakeRunner Blocking() => new(block: true);

        public Task<CodexInstallationStatus> GetInstallationStatusAsync(CancellationToken cancellationToken) =>
            Task.FromResult(InstallationStatus);

        public async Task<CodexProcessResult> RunAsync(
            string host,
            string prompt,
            CancellationToken cancellationToken)
        {
            RunCalls++;
            var running = Interlocked.Increment(ref concurrentRuns);
            MaximumConcurrentRuns = Math.Max(MaximumConcurrentRuns, running);
            FirstStarted.TrySetResult();
            if (running == 2)
            {
                TwoStarted.TrySetResult();
            }

            try
            {
                if (block)
                {
                    await Release.Task.WaitAsync(cancellationToken);
                }

                return Result;
            }
            finally
            {
                Interlocked.Decrement(ref concurrentRuns);
            }
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
