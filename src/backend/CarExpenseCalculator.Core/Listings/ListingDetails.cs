namespace CarExpenseCalculator.Core.Listings;

/// <summary>Current advertised content; labels preserve meaning without inventing registry facts.</summary>
public sealed record ListingDetails
{
    public SourcedValue<string>? Title { get; init; }
    public SourcedValue<string>? Subtitle { get; init; }
    public SourcedValue<string>? Description { get; init; }
    public SourcedValue<string>? ListingId { get; init; }
    public SourcedValue<int>? Seats { get; init; }
    public SourcedValue<int>? Doors { get; init; }
    public SourcedValue<decimal>? LuggageLitres { get; init; }
    public SourcedValue<decimal>? WeightKilograms { get; init; }
    public SourcedValue<string>? WeightLabel { get; init; }
    public SourcedValue<string>? WeightCategory { get; init; }
    public SourcedValue<decimal>? TrailerWeightKilograms { get; init; }
    public SourcedValue<string>? TrailerWeightLabel { get; init; }
    public SourcedValue<string>? TrailerWeightCategory { get; init; }
    public SourcedValue<string>? PostalCode { get; init; }
    public SourcedValue<string>? Country { get; init; }
    public SourcedValue<string>? FeeClass { get; init; }
    public SourcedValue<string>? SaleForm { get; init; }
    public SourcedValue<string>? UpdatedLocalDateTime { get; init; }
    public SourcedValue<string>? UpdatedTimeZone { get; init; }
    public SourcedValue<int>? UpdatedUtcOffsetMinutes { get; init; }

    private IReadOnlyList<SourcedValue<ListingSpecification>>? specifications;
    private IReadOnlyList<SourcedValue<SellerAnswer>>? sellerAnswers;
    public IReadOnlyList<SourcedValue<ListingSpecification>>? Specifications
    {
        get => specifications;
        init => specifications = value is null ? null : Array.AsReadOnly(value.ToArray());
    }
    public IReadOnlyList<SourcedValue<SellerAnswer>>? SellerAnswers
    {
        get => sellerAnswers;
        init => sellerAnswers = value is null ? null : Array.AsReadOnly(value.ToArray());
    }
    public IEnumerable<FieldProvenance> Provenances => new FieldProvenance?[]
    {
        Title?.Provenance,
        Subtitle?.Provenance,
        Description?.Provenance,
        ListingId?.Provenance,
        Seats?.Provenance,
        Doors?.Provenance,
        LuggageLitres?.Provenance,
        WeightKilograms?.Provenance,
        WeightLabel?.Provenance,
        WeightCategory?.Provenance,
        TrailerWeightKilograms?.Provenance,
        TrailerWeightLabel?.Provenance,
        TrailerWeightCategory?.Provenance,
        PostalCode?.Provenance,
        Country?.Provenance,
        FeeClass?.Provenance,
        SaleForm?.Provenance,
        UpdatedLocalDateTime?.Provenance,
        UpdatedTimeZone?.Provenance,
        UpdatedUtcOffsetMinutes?.Provenance,
    }.OfType<FieldProvenance>().Concat(Specifications?.Select(x => x.Provenance) ?? [])
        .Concat(SellerAnswers?.Select(x => x.Provenance) ?? []);
    public bool HasValues =>
        Title is not null ||
        Subtitle is not null ||
        Description is not null ||
        ListingId is not null ||
        Seats is not null ||
        Doors is not null ||
        LuggageLitres is not null ||
        WeightKilograms is not null ||
        WeightLabel is not null ||
        WeightCategory is not null ||
        TrailerWeightKilograms is not null ||
        TrailerWeightLabel is not null ||
        TrailerWeightCategory is not null ||
        PostalCode is not null ||
        Country is not null ||
        FeeClass is not null ||
        SaleForm is not null ||
        UpdatedLocalDateTime is not null ||
        UpdatedTimeZone is not null ||
        UpdatedUtcOffsetMinutes is not null ||
        Specifications is not null || SellerAnswers is not null;
}

public sealed record ListingSpecification(string Name, string Value);
public sealed record SellerAnswer(string Question, string Answer);
public static class ListingDetailLimits
{
    public const int DescriptionLength = 32_000;
    public const int TitleLength = 500;
    public const int LabelLength = 100;
    public const int EntryTextLength = 1_000;
    public const int Entries = 100;
}
