namespace CarExpenseCalculator.Api.Contracts.ListingAnalyses;
public sealed record ListingDetailsResponse
{
    public SourcedValueResponse<string>? Title { get; init; }
    public SourcedValueResponse<string>? Subtitle { get; init; }
    public SourcedValueResponse<string>? Description { get; init; }
    public SourcedValueResponse<string>? ListingId { get; init; }
    public SourcedValueResponse<int>? Seats { get; init; }
    public SourcedValueResponse<int>? Doors { get; init; }
    public SourcedValueResponse<decimal>? LuggageLitres { get; init; }
    public SourcedValueResponse<decimal>? WeightKilograms { get; init; }
    public SourcedValueResponse<string>? WeightLabel { get; init; }
    public SourcedValueResponse<string>? WeightCategory { get; init; }
    public SourcedValueResponse<decimal>? TrailerWeightKilograms { get; init; }
    public SourcedValueResponse<string>? TrailerWeightLabel { get; init; }
    public SourcedValueResponse<string>? TrailerWeightCategory { get; init; }
    public SourcedValueResponse<string>? PostalCode { get; init; }
    public SourcedValueResponse<string>? Country { get; init; }
    public SourcedValueResponse<string>? FeeClass { get; init; }
    public SourcedValueResponse<string>? SaleForm { get; init; }
    public SourcedValueResponse<string>? UpdatedLocalDateTime { get; init; }
    public SourcedValueResponse<string>? UpdatedTimeZone { get; init; }
    public SourcedValueResponse<int>? UpdatedUtcOffsetMinutes { get; init; }
    public IReadOnlyList<SourcedValueResponse<ListingSpecificationResponse>>? Specifications { get; init; }
    public IReadOnlyList<SourcedValueResponse<SellerAnswerResponse>>? SellerAnswers { get; init; }
}
public sealed record ListingSpecificationResponse(string Name, string Value);
public sealed record SellerAnswerResponse(string Question, string Answer);
