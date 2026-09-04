namespace CarExpenseCalculator.Extraction.Contracts;

public static class ListingExtractionContractVersions
{
    public const int Prompt = 2;

    public const int Schema = 2;
}

public static class ListingExtractionRuntime
{
    public const string RequestedModel = "gpt-5.6-luna";

    public const string ReasoningEffort = "medium";

    public const string CodexCliVersion = "0.153.0";
}

public static class ListingExtractorProblemCodes
{
    public const string InvalidRequest = "invalidListingExtractionRequest";
    public const string UnsupportedVersion = "unsupportedListingExtractionVersion";
    public const string NotConfigured = "codexNotConfigured";
    public const string RateLimited = "codexRateLimited";
    public const string TimedOut = "codexTimedOut";
    public const string ProviderUnavailable = "codexProviderUnavailable";
    public const string InvalidOutput = "codexInvalidOutput";
}

public sealed record ListingExtractionRequest(
    string NormalizedUrl,
    int PromptVersion,
    int SchemaVersion);

public sealed record ListingExtractionResponse(
    string RequestedModel,
    int PromptVersion,
    int SchemaVersion,
    DateTimeOffset AnalyzedAtUtc,
    IReadOnlyList<string> Sources,
    ExtractedListingDraft Draft);

public sealed record ListingExtractorStatusResponse(
    bool Configured,
    string RequestedModel,
    string ReasoningEffort,
    string CodexCliVersion,
    int PromptVersion,
    int SchemaVersion);

public sealed record ListingExtractorProblem(string Code);

public sealed record ExtractedEnergyConsumption(
    string? Label,
    string? Unit,
    decimal? ConsumptionPer100Kilometres);

public sealed record ExtractedListingDraft
{
    public string? RegistrationNumber { get; init; }

    public string? Make { get; init; }

    public string? Model { get; init; }

    public string? Variant { get; init; }

    public int? ModelYear { get; init; }

    public string? Vin { get; init; }

    public decimal? PriceSek { get; init; }

    public decimal? OdometerKilometres { get; init; }

    public string? SellerType { get; init; }

    public string? Locality { get; init; }

    public string? County { get; init; }

    public string? PublishedDate { get; init; }

    public string? UpdatedDate { get; init; }

    public int? ImageCount { get; init; }

    public IReadOnlyList<string>? FuelTypes { get; init; }

    public string? Transmission { get; init; }

    public string? Drivetrain { get; init; }

    public string? BodyType { get; init; }

    public string? Colour { get; init; }

    public int? Horsepower { get; init; }

    public decimal? EngineDisplacementCubicCentimetres { get; init; }

    public IReadOnlyList<ExtractedEnergyConsumption>? EnergyConsumptions { get; init; }

    public decimal? AnnualVehicleTaxSek { get; init; }

    public int? OwnerCount { get; init; }

    public string? FirstRegistrationDate { get; init; }

    public string? LastInspectionDate { get; init; }

    public string? NextInspectionDate { get; init; }

    public bool? TowBar { get; init; }

    public IReadOnlyList<string>? Equipment { get; init; }

    public IReadOnlyList<string>? SellerClaims { get; init; }

    public IReadOnlyList<string>? ConditionNotes { get; init; }
}
