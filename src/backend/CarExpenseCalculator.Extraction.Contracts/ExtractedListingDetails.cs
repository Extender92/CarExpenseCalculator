namespace CarExpenseCalculator.Extraction.Contracts;

public sealed record ExtractedListingDetails
{
    public string? Title { get; init; }
    public string? Subtitle { get; init; }
    public string? Description { get; init; }
    public string? ListingId { get; init; }
    public int? Seats { get; init; }
    public int? Doors { get; init; }
    public decimal? LuggageLitres { get; init; }
    public decimal? WeightKilograms { get; init; }
    public string? WeightLabel { get; init; }
    public string? WeightCategory { get; init; }
    public decimal? TrailerWeightKilograms { get; init; }
    public string? TrailerWeightLabel { get; init; }
    public string? TrailerWeightCategory { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
    public string? FeeClass { get; init; }
    public string? SaleForm { get; init; }
    public string? UpdatedLocalDateTime { get; init; }
    public string? UpdatedTimeZone { get; init; }
    public int? UpdatedUtcOffsetMinutes { get; init; }
    public IReadOnlyList<ExtractedListingSpecification>? Specifications { get; init; }
    public IReadOnlyList<ExtractedSellerAnswer>? SellerAnswers { get; init; }
}
public sealed record ExtractedListingSpecification(string Name, string Value);
public sealed record ExtractedSellerAnswer(string Question, string Answer);
