using System.Text.Json;
using System.Text.Json.Serialization;
using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Core.Listings;

namespace CarExpenseCalculator.Infrastructure.Persistence.SavedListings;

internal static class SavedListingJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string SerializeProvenance(ListingDraft listing)
    {
        var values = new Dictionary<string, ProvenanceSnapshot>(StringComparer.Ordinal);
        Add(values, "registrationNumber", listing.RegistrationNumber?.Provenance);
        Add(values, "vehicleLabel", listing.VehicleLabel?.Provenance);
        Add(values, "make", listing.Make?.Provenance);
        Add(values, "model", listing.Model?.Provenance);
        Add(values, "variant", listing.Variant?.Provenance);
        Add(values, "modelYear", listing.ModelYear?.Provenance);
        Add(values, "vin", listing.Vin?.Provenance);
        Add(values, "priceSek", listing.PriceSek?.Provenance);
        Add(values, "odometerKilometres", listing.OdometerKilometres?.Provenance);
        Add(values, "sellerType", listing.SellerType?.Provenance);
        Add(values, "locality", listing.Locality?.Provenance);
        Add(values, "county", listing.County?.Provenance);
        Add(values, "publishedDate", listing.PublishedDate?.Provenance);
        Add(values, "updatedDate", listing.UpdatedDate?.Provenance);
        Add(values, "imageCount", listing.ImageCount?.Provenance);
        Add(values, "fuelTypes", listing.FuelTypes?.Provenance);
        Add(values, "transmission", listing.Transmission?.Provenance);
        Add(values, "drivetrain", listing.Drivetrain?.Provenance);
        Add(values, "bodyType", listing.BodyType?.Provenance);
        Add(values, "colour", listing.Colour?.Provenance);
        Add(values, "horsepower", listing.Horsepower?.Provenance);
        Add(values, "engineDisplacementCubicCentimetres", listing.EngineDisplacementCubicCentimetres?.Provenance);
        Add(values, "energyConsumptions", listing.EnergyConsumptions?.Provenance);
        Add(values, "annualVehicleTaxSek", listing.AnnualVehicleTaxSek?.Provenance);
        Add(values, "ownerCount", listing.OwnerCount?.Provenance);
        Add(values, "firstRegistrationDate", listing.FirstRegistrationDate?.Provenance);
        Add(values, "lastInspectionDate", listing.LastInspectionDate?.Provenance);
        Add(values, "nextInspectionDate", listing.NextInspectionDate?.Provenance);
        Add(values, "towBar", listing.TowBar?.Provenance);
        Add(values, "equipment", listing.Equipment?.Provenance);
        Add(values, "sellerClaims", listing.SellerClaims?.Provenance);
        Add(values, "conditionNotes", listing.ConditionNotes?.Provenance);
        return JsonSerializer.Serialize(values, Options);
    }

    public static IReadOnlyDictionary<string, FieldProvenance> DeserializeProvenance(
        string json,
        ListingUrl sourceUrl)
    {
        var values = JsonSerializer.Deserialize<Dictionary<string, ProvenanceSnapshot>>(json, Options)
            ?? throw new JsonException("Saved listing provenance is missing.");
        return values.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToCore(sourceUrl),
            StringComparer.Ordinal);
    }

    public static string? SerializeEnergyConsumptions(SourcedCollection<EnergyConsumption>? values) =>
        values is null ? null : JsonSerializer.Serialize(values.Values, Options);

    public static IReadOnlyList<EnergyConsumption> DeserializeEnergyConsumptions(string json) =>
        Array.AsReadOnly(
            JsonSerializer.Deserialize<EnergyConsumption[]>(json, Options)
            ?? throw new JsonException("Saved energy consumptions are missing."));

    public static string? SerializeStrings(SourcedCollection<string>? values) =>
        values is null ? null : JsonSerializer.Serialize(values.Values, Options);

    public static IReadOnlyList<string> DeserializeStrings(string json) =>
        Array.AsReadOnly(
            JsonSerializer.Deserialize<string[]>(json, Options)
            ?? throw new JsonException("Saved listing collection is missing."));

    private static void Add(
        IDictionary<string, ProvenanceSnapshot> values,
        string name,
        FieldProvenance? provenance)
    {
        if (provenance is not null)
        {
            values.Add(
                name,
                new ProvenanceSnapshot(
                    provenance.Origin,
                    provenance.ExtractionMethod,
                    provenance.Verification));
        }
    }

    private sealed record ProvenanceSnapshot(
        FieldOrigin Origin,
        ExtractionMethod ExtractionMethod,
        VerificationStatus Verification)
    {
        public FieldProvenance ToCore(ListingUrl sourceUrl) =>
            new(Origin, ExtractionMethod, Verification, sourceUrl);
    }
}
