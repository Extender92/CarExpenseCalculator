using CarExpenseCalculator.Extraction.Contracts;

namespace CarExpenseCalculator.CodexExtractor;

public sealed record CodexExtractorOptions
{
    public const string RequiredModel = ListingExtractionRuntime.RequestedModel;
    public const string RequiredReasoningEffort = ListingExtractionRuntime.ReasoningEffort;
    public const string RequiredCliVersion = ListingExtractionRuntime.CodexCliVersion;

    public string Model { get; init; } = RequiredModel;

    public string ReasoningEffort { get; init; } = RequiredReasoningEffort;

    public string? CodexHome { get; init; }

    public string CodexExecutable { get; init; } = "codex";

    public string WorkRoot { get; init; } = Path.Combine(Path.GetTempPath(), "car-expense-codex-work");

    public string SchemaPath { get; init; } = string.Empty;

    public int PromptVersion { get; init; } = ListingExtractionContractVersions.Prompt;

    public int SchemaVersion { get; init; } = ListingExtractionContractVersions.Schema;

    internal TimeSpan OperationTimeout { get; init; } = TimeSpan.FromSeconds(60);

    public bool HasValidOwnedConfiguration =>
        string.Equals(Model, RequiredModel, StringComparison.Ordinal)
        && string.Equals(ReasoningEffort, RequiredReasoningEffort, StringComparison.Ordinal)
        && !string.IsNullOrWhiteSpace(CodexHome)
        && !string.IsNullOrWhiteSpace(CodexExecutable)
        && !string.IsNullOrWhiteSpace(WorkRoot)
        && File.Exists(SchemaPath)
        && PromptVersion == ListingExtractionContractVersions.Prompt
        && SchemaVersion == ListingExtractionContractVersions.Schema
        && OperationTimeout > TimeSpan.Zero;
}
