using CarExpenseCalculator.Core.Listings;
namespace CarExpenseCalculator.Infrastructure.Persistence.Households;
internal sealed record DraftListingDetails
{
    public DraftFact<string>? Title { get; init; }
    public DraftFact<string>? Subtitle { get; init; }
    public DraftFact<string>? Description { get; init; }
    public DraftFact<string>? ListingId { get; init; }
    public DraftFact<int>? Seats { get; init; }
    public DraftFact<int>? Doors { get; init; }
    public DraftFact<decimal>? LuggageLitres { get; init; }
    public DraftFact<decimal>? WeightKilograms { get; init; }
    public DraftFact<string>? WeightLabel { get; init; }
    public DraftFact<string>? WeightCategory { get; init; }
    public DraftFact<decimal>? TrailerWeightKilograms { get; init; }
    public DraftFact<string>? TrailerWeightLabel { get; init; }
    public DraftFact<string>? TrailerWeightCategory { get; init; }
    public DraftFact<string>? PostalCode { get; init; }
    public DraftFact<string>? Country { get; init; }
    public DraftFact<string>? FeeClass { get; init; }
    public DraftFact<string>? SaleForm { get; init; }
    public DraftFact<string>? UpdatedLocalDateTime { get; init; }
    public DraftFact<string>? UpdatedTimeZone { get; init; }
    public DraftFact<int>? UpdatedUtcOffsetMinutes { get; init; }
    public DraftFact<DraftSpecification>[]? Specifications { get; init; }
    public DraftFact<DraftSellerAnswer>[]? SellerAnswers { get; init; }
    public static DraftListingDetails? FromCore(ListingDetails? x) => x is null ? null : new()
    {
        Title = DraftFact<string>.FromCore(x.Title),
        Subtitle = DraftFact<string>.FromCore(x.Subtitle),
        Description = DraftFact<string>.FromCore(x.Description),
        ListingId = DraftFact<string>.FromCore(x.ListingId),
        Seats = DraftFact<int>.FromCore(x.Seats),
        Doors = DraftFact<int>.FromCore(x.Doors),
        LuggageLitres = DraftFact<decimal>.FromCore(x.LuggageLitres),
        WeightKilograms = DraftFact<decimal>.FromCore(x.WeightKilograms),
        WeightLabel = DraftFact<string>.FromCore(x.WeightLabel),
        WeightCategory = DraftFact<string>.FromCore(x.WeightCategory),
        TrailerWeightKilograms = DraftFact<decimal>.FromCore(x.TrailerWeightKilograms),
        TrailerWeightLabel = DraftFact<string>.FromCore(x.TrailerWeightLabel),
        TrailerWeightCategory = DraftFact<string>.FromCore(x.TrailerWeightCategory),
        PostalCode = DraftFact<string>.FromCore(x.PostalCode),
        Country = DraftFact<string>.FromCore(x.Country),
        FeeClass = DraftFact<string>.FromCore(x.FeeClass),
        SaleForm = DraftFact<string>.FromCore(x.SaleForm),
        UpdatedLocalDateTime = DraftFact<string>.FromCore(x.UpdatedLocalDateTime),
        UpdatedTimeZone = DraftFact<string>.FromCore(x.UpdatedTimeZone),
        UpdatedUtcOffsetMinutes = DraftFact<int>.FromCore(x.UpdatedUtcOffsetMinutes),
        Specifications = x.Specifications?.Select(v => new DraftFact<DraftSpecification>(new(v.Value.Name, v.Value.Value), DraftProvenance.FromCore(v.Provenance))).ToArray(),
        SellerAnswers = x.SellerAnswers?.Select(v => new DraftFact<DraftSellerAnswer>(new(v.Value.Question, v.Value.Answer), DraftProvenance.FromCore(v.Provenance))).ToArray(),
    };
    public ListingDetails ToCore() => new()
    {
        Title = Title?.ToCore(),
        Subtitle = Subtitle?.ToCore(),
        Description = Description?.ToCore(),
        ListingId = ListingId?.ToCore(),
        Seats = Seats?.ToCore(),
        Doors = Doors?.ToCore(),
        LuggageLitres = LuggageLitres?.ToCore(),
        WeightKilograms = WeightKilograms?.ToCore(),
        WeightLabel = WeightLabel?.ToCore(),
        WeightCategory = WeightCategory?.ToCore(),
        TrailerWeightKilograms = TrailerWeightKilograms?.ToCore(),
        TrailerWeightLabel = TrailerWeightLabel?.ToCore(),
        TrailerWeightCategory = TrailerWeightCategory?.ToCore(),
        PostalCode = PostalCode?.ToCore(),
        Country = Country?.ToCore(),
        FeeClass = FeeClass?.ToCore(),
        SaleForm = SaleForm?.ToCore(),
        UpdatedLocalDateTime = UpdatedLocalDateTime?.ToCore(),
        UpdatedTimeZone = UpdatedTimeZone?.ToCore(),
        UpdatedUtcOffsetMinutes = UpdatedUtcOffsetMinutes?.ToCore(),
        Specifications = Specifications?.Select(v => new SourcedValue<ListingSpecification>(new(v.Value.Name, v.Value.Value), v.Provenance.ToCore())).ToArray(),
        SellerAnswers = SellerAnswers?.Select(v => new SourcedValue<SellerAnswer>(new(v.Value.Question, v.Value.Answer), v.Provenance.ToCore())).ToArray(),
    };
}
internal sealed record DraftSpecification(string Name, string Value);
internal sealed record DraftSellerAnswer(string Question, string Answer);
