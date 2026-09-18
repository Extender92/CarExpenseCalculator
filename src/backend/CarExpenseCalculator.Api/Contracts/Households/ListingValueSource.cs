using System.Text.Json.Serialization;
using CarExpenseCalculator.Api.Contracts.ManualCalculations;

namespace CarExpenseCalculator.Api.Contracts.Households;

[JsonConverter(typeof(StrictStringEnumConverter<ListingReuseField>))]
public enum ListingReuseField { PriceSek, AnnualVehicleTaxSek, FuelTypes, EnergyConsumptions }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ListingValueSource
{
    public required string ListingReference { get; init; }
    public required ListingReuseField Field { get; init; }
    public string? OriginalLabel { get; init; }
    public long? ListingVersion { get; init; }
    public int? ItemIndex { get; init; }
}
