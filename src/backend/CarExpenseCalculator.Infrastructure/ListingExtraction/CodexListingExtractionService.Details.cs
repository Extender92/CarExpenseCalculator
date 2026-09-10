using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Extraction.Contracts;
namespace CarExpenseCalculator.Infrastructure.ListingExtraction;
internal sealed partial class CodexListingExtractionService
{
    private static ListingDetails? MapDetails(ExtractedListingDetails? x, FieldProvenance p) => x is null ? null : new()
    {
        Title = Value(x.Title, p),
        Subtitle = Value(x.Subtitle, p),
        Description = Value(x.Description, p),
        ListingId = Value(x.ListingId, p),
        Seats = Value(x.Seats, p),
        Doors = Value(x.Doors, p),
        LuggageLitres = Value(x.LuggageLitres, p),
        WeightKilograms = Value(x.WeightKilograms, p),
        WeightLabel = Value(x.WeightLabel, p),
        WeightCategory = Value(x.WeightCategory, p),
        TrailerWeightKilograms = Value(x.TrailerWeightKilograms, p),
        TrailerWeightLabel = Value(x.TrailerWeightLabel, p),
        TrailerWeightCategory = Value(x.TrailerWeightCategory, p),
        PostalCode = Value(x.PostalCode, p),
        Country = Value(x.Country, p),
        FeeClass = Value(x.FeeClass, p),
        SaleForm = Value(x.SaleForm, p),
        UpdatedLocalDateTime = Value(x.UpdatedLocalDateTime, p),
        UpdatedTimeZone = Value(x.UpdatedTimeZone, p),
        UpdatedUtcOffsetMinutes = Value(x.UpdatedUtcOffsetMinutes, p),
        Specifications = x.Specifications?.Select(v => new SourcedValue<ListingSpecification>(new(v.Name, v.Value), p)).ToArray(),
        SellerAnswers = x.SellerAnswers?.Select(v => new SourcedValue<SellerAnswer>(new(v.Question, v.Answer), p)).ToArray(),
    };
}
