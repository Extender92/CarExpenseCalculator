using CarExpenseCalculator.Extraction.Contracts;

namespace CarExpenseCalculator.CodexExtractor;

internal sealed record CodexInstallationStatus(
    bool HasRequiredVersion,
    bool HasChatGptAuthentication);

internal sealed record CodexProcessResult(
    int ExitCode,
    IReadOnlyList<string> StandardOutputLines,
    string StandardError,
    bool OutputLimitExceeded);

internal interface ICodexProcessRunner
{
    Task<CodexInstallationStatus> GetInstallationStatusAsync(CancellationToken cancellationToken);

    Task<CodexProcessResult> RunAsync(
        string host,
        string prompt,
        CancellationToken cancellationToken);
}

internal sealed record ParsedCodexOutput(
    IReadOnlyList<string> Sources,
    ExtractedListingDraft Draft,
    int EventCount);

internal enum CodexExecutionFailure
{
    InvalidRequest,
    UnsupportedVersion,
    NotConfigured,
    RateLimited,
    TimedOut,
    ProviderUnavailable,
    InvalidOutput,
}

internal abstract record CodexExtractionExecution;

internal sealed record CodexExtractionSucceeded(ListingExtractionResponse Response)
    : CodexExtractionExecution;

internal sealed record CodexExtractionFailed(CodexExecutionFailure Failure)
    : CodexExtractionExecution;
