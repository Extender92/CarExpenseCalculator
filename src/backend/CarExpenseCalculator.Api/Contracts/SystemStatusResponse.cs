namespace CarExpenseCalculator.Api.Contracts;

public sealed record SystemStatusResponse(
    string Status,
    string Version,
    string Database,
    FeatureStatusResponse Features);

public sealed record FeatureStatusResponse(
    bool RuleBasedSearch,
    bool UrlAnalysis,
    bool ManualCalculator,
    bool AiReview);
