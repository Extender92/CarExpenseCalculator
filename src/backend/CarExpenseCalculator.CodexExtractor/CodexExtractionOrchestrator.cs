using System.Diagnostics;
using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Extraction.Contracts;

namespace CarExpenseCalculator.CodexExtractor;

internal sealed class CodexExtractionOrchestrator
{
    private readonly CodexExtractorOptions options;
    private readonly ICodexProcessRunner processRunner;
    private readonly CodexJsonlParser parser;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<CodexExtractionOrchestrator> logger;
    private readonly SemaphoreSlim concurrencyGate;

    public CodexExtractionOrchestrator(
        CodexExtractorOptions options,
        ICodexProcessRunner processRunner,
        CodexJsonlParser parser,
        TimeProvider timeProvider,
        ILogger<CodexExtractionOrchestrator> logger)
        : this(
            options,
            processRunner,
            parser,
            timeProvider,
            logger,
            new SemaphoreSlim(initialCount: 2, maxCount: 2))
    {
    }

    internal CodexExtractionOrchestrator(
        CodexExtractorOptions options,
        ICodexProcessRunner processRunner,
        CodexJsonlParser parser,
        TimeProvider timeProvider,
        ILogger<CodexExtractionOrchestrator> logger,
        SemaphoreSlim concurrencyGate)
    {
        this.options = options;
        this.processRunner = processRunner;
        this.parser = parser;
        this.timeProvider = timeProvider;
        this.logger = logger;
        this.concurrencyGate = concurrencyGate;
    }

    public async Task<CodexExtractionExecution> ExecuteAsync(
        ListingExtractionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var correlationId = Guid.NewGuid().ToString("N");
        var stopwatch = Stopwatch.StartNew();
        var outcome = "unknown";
        var eventCount = 0;

        try
        {
            if (!TryValidateRequest(request, out var listingUrl, out var requestFailure))
            {
                outcome = requestFailure.ToString();
                return new CodexExtractionFailed(requestFailure);
            }

            if (!options.HasValidOwnedConfiguration)
            {
                outcome = CodexExecutionFailure.NotConfigured.ToString();
                return new CodexExtractionFailed(CodexExecutionFailure.NotConfigured);
            }

            using var operationTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            operationTimeout.CancelAfter(options.OperationTimeout);

            var gateEntered = false;
            try
            {
                await concurrencyGate.WaitAsync(operationTimeout.Token);
                gateEntered = true;

                var installation = await processRunner.GetInstallationStatusAsync(operationTimeout.Token);
                if (!installation.HasRequiredVersion || !installation.HasChatGptAuthentication)
                {
                    outcome = CodexExecutionFailure.NotConfigured.ToString();
                    return new CodexExtractionFailed(CodexExecutionFailure.NotConfigured);
                }

                var prompt = ListingExtractionPrompt.Create(listingUrl!);
                var processResult = await processRunner.RunAsync(
                    listingUrl!.Host,
                    prompt,
                    operationTimeout.Token);
                if (processResult.OutputLimitExceeded)
                {
                    outcome = CodexExecutionFailure.InvalidOutput.ToString();
                    return new CodexExtractionFailed(CodexExecutionFailure.InvalidOutput);
                }

                if (processResult.ExitCode != 0)
                {
                    var failure = IsRateLimit(processResult.StandardError)
                        ? CodexExecutionFailure.RateLimited
                        : CodexExecutionFailure.ProviderUnavailable;
                    outcome = failure.ToString();
                    return new CodexExtractionFailed(failure);
                }

                if (!parser.TryParse(
                        processResult.StandardOutputLines,
                        out var parsed,
                        out var runtimeFailure))
                {
                    var failure = IsRateLimit(runtimeFailure)
                        ? CodexExecutionFailure.RateLimited
                        : CodexExecutionFailure.InvalidOutput;
                    outcome = failure.ToString();
                    return new CodexExtractionFailed(failure);
                }

                eventCount = parsed!.EventCount;
                outcome = "succeeded";
                return new CodexExtractionSucceeded(
                    new ListingExtractionResponse(
                        options.Model,
                        options.PromptVersion,
                        options.SchemaVersion,
                        timeProvider.GetUtcNow(),
                        parsed.Sources,
                        parsed.Draft));
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                outcome = CodexExecutionFailure.TimedOut.ToString();
                return new CodexExtractionFailed(CodexExecutionFailure.TimedOut);
            }
            catch (Exception exception) when (
                exception is InvalidOperationException
                or IOException
                or System.ComponentModel.Win32Exception)
            {
                outcome = CodexExecutionFailure.ProviderUnavailable.ToString();
                return new CodexExtractionFailed(CodexExecutionFailure.ProviderUnavailable);
            }
            finally
            {
                if (gateEntered)
                {
                    concurrencyGate.Release();
                }
            }
        }
        finally
        {
            logger.LogInformation(
                "Codex extraction {CorrelationId} finished with {OutcomeCode} in {DurationMilliseconds} ms after {EventCount} events using requested model {RequestedModel}.",
                correlationId,
                outcome,
                stopwatch.ElapsedMilliseconds,
                eventCount,
                options.HasValidOwnedConfiguration
                    ? options.Model
                    : CodexExtractorOptions.RequiredModel);
        }
    }

    public async Task<ListingExtractorStatusResponse> GetStatusAsync(CancellationToken cancellationToken)
    {
        if (!options.HasValidOwnedConfiguration)
        {
            return CreateStatus(configured: false);
        }

        try
        {
            var installation = await processRunner.GetInstallationStatusAsync(cancellationToken);
            return CreateStatus(
                installation.HasRequiredVersion && installation.HasChatGptAuthentication);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CreateStatus(configured: false);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or IOException
            or System.ComponentModel.Win32Exception)
        {
            return CreateStatus(configured: false);
        }
    }

    private ListingExtractorStatusResponse CreateStatus(bool configured)
    {
        return new ListingExtractorStatusResponse(
            configured,
            CodexExtractorOptions.RequiredModel,
            CodexExtractorOptions.RequiredReasoningEffort,
            CodexExtractorOptions.RequiredCliVersion,
            options.PromptVersion,
            options.SchemaVersion);
    }

    private static bool TryValidateRequest(
        ListingExtractionRequest request,
        out ListingUrl? listingUrl,
        out CodexExecutionFailure failure)
    {
        listingUrl = null;
        if (request.PromptVersion != ListingExtractionContractVersions.Prompt
            || request.SchemaVersion != ListingExtractionContractVersions.Schema)
        {
            failure = CodexExecutionFailure.UnsupportedVersion;
            return false;
        }

        if (!ListingUrl.TryParse(request.NormalizedUrl, out listingUrl)
            || !listingUrl!.Value.Equals(request.NormalizedUrl, StringComparison.Ordinal))
        {
            failure = CodexExecutionFailure.InvalidRequest;
            return false;
        }

        failure = default;
        return true;
    }

    private static bool IsRateLimit(string? value)
    {
        return value?.Contains("429", StringComparison.OrdinalIgnoreCase) == true
            || value?.Contains("rate limit", StringComparison.OrdinalIgnoreCase) == true
            || value?.Contains("usage limit", StringComparison.OrdinalIgnoreCase) == true
            || value?.Contains("quota", StringComparison.OrdinalIgnoreCase) == true;
    }
}
