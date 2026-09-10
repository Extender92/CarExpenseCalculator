using C = CarExpenseCalculator.Core.Listings;
using A = CarExpenseCalculator.Api.Contracts.ListingAnalyses;
using I = CarExpenseCalculator.Api.Contracts.SavedListings;
namespace CarExpenseCalculator.Api.Mapping;
internal static class ListingDetailsMapper
{
    public static A.ListingDetailsResponse? ToResponse(C.ListingDetails? x) => x is null ? null : new()
    {
        Title = Response(x.Title, v => v),
        Subtitle = Response(x.Subtitle, v => v),
        Description = Response(x.Description, v => v),
        ListingId = Response(x.ListingId, v => v),
        Seats = Response(x.Seats, v => v),
        Doors = Response(x.Doors, v => v),
        LuggageLitres = Response(x.LuggageLitres, v => v),
        WeightKilograms = Response(x.WeightKilograms, v => v),
        WeightLabel = Response(x.WeightLabel, v => v),
        WeightCategory = Response(x.WeightCategory, v => v),
        TrailerWeightKilograms = Response(x.TrailerWeightKilograms, v => v),
        TrailerWeightLabel = Response(x.TrailerWeightLabel, v => v),
        TrailerWeightCategory = Response(x.TrailerWeightCategory, v => v),
        PostalCode = Response(x.PostalCode, v => v),
        Country = Response(x.Country, v => v),
        FeeClass = Response(x.FeeClass, v => v),
        SaleForm = Response(x.SaleForm, v => v),
        UpdatedLocalDateTime = Response(x.UpdatedLocalDateTime, v => v),
        UpdatedTimeZone = Response(x.UpdatedTimeZone, v => v),
        UpdatedUtcOffsetMinutes = Response(x.UpdatedUtcOffsetMinutes, v => v),
        Specifications = x.Specifications?.Select(v => Response(v, a => new A.ListingSpecificationResponse(a.Name, a.Value))!).ToArray(),
        SellerAnswers = x.SellerAnswers?.Select(v => Response(v, a => new A.SellerAnswerResponse(a.Question, a.Answer))!).ToArray(),
    };
    public static I.ListingDetailsInput? ToInput(C.ListingDetails? x) => x is null ? null : new()
    {
        Title = Input(x.Title, v => v),
        Subtitle = Input(x.Subtitle, v => v),
        Description = Input(x.Description, v => v),
        ListingId = Input(x.ListingId, v => v),
        Seats = Input(x.Seats, v => v),
        Doors = Input(x.Doors, v => v),
        LuggageLitres = Input(x.LuggageLitres, v => v),
        WeightKilograms = Input(x.WeightKilograms, v => v),
        WeightLabel = Input(x.WeightLabel, v => v),
        WeightCategory = Input(x.WeightCategory, v => v),
        TrailerWeightKilograms = Input(x.TrailerWeightKilograms, v => v),
        TrailerWeightLabel = Input(x.TrailerWeightLabel, v => v),
        TrailerWeightCategory = Input(x.TrailerWeightCategory, v => v),
        PostalCode = Input(x.PostalCode, v => v),
        Country = Input(x.Country, v => v),
        FeeClass = Input(x.FeeClass, v => v),
        SaleForm = Input(x.SaleForm, v => v),
        UpdatedLocalDateTime = Input(x.UpdatedLocalDateTime, v => v),
        UpdatedTimeZone = Input(x.UpdatedTimeZone, v => v),
        UpdatedUtcOffsetMinutes = Input(x.UpdatedUtcOffsetMinutes, v => v),
        Specifications = x.Specifications?.Select(v => Input(v, a => new I.ListingSpecificationInput(a.Name, a.Value))!).ToArray(),
        SellerAnswers = x.SellerAnswers?.Select(v => Input(v, a => new I.SellerAnswerInput(a.Question, a.Answer))!).ToArray(),
    };
    public static C.ListingDetails? ToCore(I.ListingDetailsInput? x, ICollection<C.ListingValidationError> errors) => x is null ? null : new()
    {
        Title = Core(x.Title, "draft.details.title", errors, v => v),
        Subtitle = Core(x.Subtitle, "draft.details.subtitle", errors, v => v),
        Description = Core(x.Description, "draft.details.description", errors, v => v),
        ListingId = Core(x.ListingId, "draft.details.listingId", errors, v => v),
        Seats = Core(x.Seats, "draft.details.seats", errors, v => v),
        Doors = Core(x.Doors, "draft.details.doors", errors, v => v),
        LuggageLitres = Core(x.LuggageLitres, "draft.details.luggageLitres", errors, v => v),
        WeightKilograms = Core(x.WeightKilograms, "draft.details.weightKilograms", errors, v => v),
        WeightLabel = Core(x.WeightLabel, "draft.details.weightLabel", errors, v => v),
        WeightCategory = Core(x.WeightCategory, "draft.details.weightCategory", errors, v => v),
        TrailerWeightKilograms = Core(x.TrailerWeightKilograms, "draft.details.trailerWeightKilograms", errors, v => v),
        TrailerWeightLabel = Core(x.TrailerWeightLabel, "draft.details.trailerWeightLabel", errors, v => v),
        TrailerWeightCategory = Core(x.TrailerWeightCategory, "draft.details.trailerWeightCategory", errors, v => v),
        PostalCode = Core(x.PostalCode, "draft.details.postalCode", errors, v => v),
        Country = Core(x.Country, "draft.details.country", errors, v => v),
        FeeClass = Core(x.FeeClass, "draft.details.feeClass", errors, v => v),
        SaleForm = Core(x.SaleForm, "draft.details.saleForm", errors, v => v),
        UpdatedLocalDateTime = Core(x.UpdatedLocalDateTime, "draft.details.updatedLocalDateTime", errors, v => v),
        UpdatedTimeZone = Core(x.UpdatedTimeZone, "draft.details.updatedTimeZone", errors, v => v),
        UpdatedUtcOffsetMinutes = Core(x.UpdatedUtcOffsetMinutes, "draft.details.updatedUtcOffsetMinutes", errors, v => v),
        Specifications = x.Specifications?.Select((v, i) => Core(v, $"draft.details.specifications[{i}]", errors, a => new C.ListingSpecification(a.Name, a.Value))!).ToArray(),
        SellerAnswers = x.SellerAnswers?.Select((v, i) => Core(v, $"draft.details.sellerAnswers[{i}]", errors, a => new C.SellerAnswer(a.Question, a.Answer))!).ToArray(),
    };
    private static A.SourcedValueResponse<TOut>? Response<TIn,TOut>(C.SourcedValue<TIn>? x, Func<TIn,TOut> map)
        where TIn : notnull where TOut : notnull => x is null ? null : new(map(x.Value), new(
            (A.FieldOrigin)x.Provenance.Origin, (A.ExtractionMethod)x.Provenance.ExtractionMethod,
            (A.VerificationStatus)x.Provenance.Verification, x.Provenance.SourceUrl.Value));
    private static I.SourcedValueInput<TOut>? Input<TIn,TOut>(C.SourcedValue<TIn>? x, Func<TIn,TOut> map)
        where TIn : notnull where TOut : notnull => x is null ? null : new() { Value = map(x.Value), Provenance = new() {
            Origin = (A.FieldOrigin)x.Provenance.Origin, ExtractionMethod = (A.ExtractionMethod)x.Provenance.ExtractionMethod,
            Verification = (A.VerificationStatus)x.Provenance.Verification, SourceUrl = x.Provenance.SourceUrl.Value } };
    private static C.SourcedValue<TOut>? Core<TIn,TOut>(I.SourcedValueInput<TIn>? x, string path,
        ICollection<C.ListingValidationError> errors, Func<TIn,TOut> map) where TIn : notnull where TOut : notnull
    {
        if (x is null) return null;
        if (x.Provenance is null || !C.ListingUrl.TryParse(x.Provenance.SourceUrl, out var url))
        { errors.Add(new(path + ".provenance.sourceUrl", "A valid source reference is required.")); return null; }
        return new(map(x.Value), new((C.FieldOrigin)x.Provenance.Origin, (C.ExtractionMethod)x.Provenance.ExtractionMethod,
            (C.VerificationStatus)x.Provenance.Verification, url!));
    }
}
