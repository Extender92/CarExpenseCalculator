using System.ComponentModel.DataAnnotations;
namespace CarExpenseCalculator.Api.Contracts.SavedListings;
public sealed record ListingDetailsInput
{
    public SourcedValueInput<string>? Title { get; init; }
    public SourcedValueInput<string>? Subtitle { get; init; }
    public SourcedValueInput<string>? Description { get; init; }
    public SourcedValueInput<string>? ListingId { get; init; }
    public SourcedValueInput<int>? Seats { get; init; }
    public SourcedValueInput<int>? Doors { get; init; }
    public SourcedValueInput<decimal>? LuggageLitres { get; init; }
    public SourcedValueInput<decimal>? WeightKilograms { get; init; }
    public SourcedValueInput<string>? WeightLabel { get; init; }
    public SourcedValueInput<string>? WeightCategory { get; init; }
    public SourcedValueInput<decimal>? TrailerWeightKilograms { get; init; }
    public SourcedValueInput<string>? TrailerWeightLabel { get; init; }
    public SourcedValueInput<string>? TrailerWeightCategory { get; init; }
    public SourcedValueInput<string>? PostalCode { get; init; }
    public SourcedValueInput<string>? Country { get; init; }
    public SourcedValueInput<string>? FeeClass { get; init; }
    public SourcedValueInput<string>? SaleForm { get; init; }
    public SourcedValueInput<string>? UpdatedLocalDateTime { get; init; }
    public SourcedValueInput<string>? UpdatedTimeZone { get; init; }
    public SourcedValueInput<int>? UpdatedUtcOffsetMinutes { get; init; }
    public IReadOnlyList<SourcedValueInput<ListingSpecificationInput>>? Specifications { get; init; }
    public IReadOnlyList<SourcedValueInput<SellerAnswerInput>>? SellerAnswers { get; init; }
}
public sealed record ListingSpecificationInput([Required] string Name, [Required] string Value);
public sealed record SellerAnswerInput([Required] string Question, [Required] string Answer);
