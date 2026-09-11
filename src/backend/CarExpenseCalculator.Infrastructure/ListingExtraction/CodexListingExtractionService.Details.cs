using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Extraction.Contracts;
namespace CarExpenseCalculator.Infrastructure.ListingExtraction;
internal sealed partial class CodexListingExtractionService
{
    private static ListingDraft ApplyRetrievedContent(ListingDraft draft, RetrievedListingContent content, FieldProvenance provenance) => draft with
    {
        Equipment = content.Equipment is null ? null : new SourcedCollection<string>(content.Equipment, provenance),
        Details = (draft.Details ?? new ListingDetails()) with
        {
            Title = Value(content.Title, provenance),
            Subtitle = Value(content.Subtitle, provenance),
            Description = Value(content.Description, provenance),
            ListingId = Value(content.ListingId, provenance),
            Specifications = content.Specifications?.Select(x => new SourcedValue<ListingSpecification>(new(x.Name, x.Value), provenance)).ToArray(),
            SellerAnswers = content.SellerAnswers?.Select(x => new SourcedValue<SellerAnswer>(new(x.Question, x.Answer), provenance)).ToArray(),
        },
    };

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
