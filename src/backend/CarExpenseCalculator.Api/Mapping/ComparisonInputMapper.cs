using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using A = CarExpenseCalculator.Api.Contracts.Comparisons;
using C = CarExpenseCalculator.Core.Comparisons;
using S = CarExpenseCalculator.Infrastructure.Persistence.Comparisons;
using L = CarExpenseCalculator.Core.Listings;
using AL = CarExpenseCalculator.Api.Contracts.ListingAnalyses;

namespace CarExpenseCalculator.Api.Mapping;

internal static class ComparisonInputMapper
{
    public static string FactWritePath(string path) => path is "expectedListingVersion" or "costConfirmation" or "reviewCurrentListing"
        ? path : "edits." + path;
    public static C.RuleProfileInput NormalizeRules(A.RuleProfileInput input, string path)
    {
        try { return new C.RuleProfileProcessor().Normalize(ToCore(input)); }
        catch (C.ComparisonInputValidationException e)
        { throw new C.ComparisonInputValidationException(e.Errors.Select(x => new C.ComparisonInputError(path + "." + x.Path, x.Code, x.Message))); }
    }
    public static S.VehicleFactsWrite ToStore(A.VehicleFactsWrite x) => new(ToStore(x.Edits),
        x.ExpectedListingVersion, x.ReviewCurrentListing, (S.CostConfirmationAction)x.CostConfirmation);
    public static S.VehicleFactEdits ToStore(A.VehicleFactEdits x) => new()
    {
        PurchasePriceSek = Edit(x.PurchasePriceSek, v => v),
        OdometerKilometres = Edit(x.OdometerKilometres, v => v),
        OwnerCount = Edit(x.OwnerCount, v => v),
        TowBar = Edit(x.TowBar, v => v),
        Transmission = Edit(x.Transmission, v => (L.Transmission)v),
        Seats = Edit(x.Seats, v => v),
        ModelYear = Edit(x.ModelYear, v => v),
        FuelTypes = Edit(x.FuelTypes, v => new C.FuelTypeSet(v.Select(x => (L.FuelType)x))),
        BodyType = Edit(x.BodyType, v => (L.BodyType)v),
        Drivetrain = Edit(x.Drivetrain, v => (L.Drivetrain)v),
        Locality = Edit(x.Locality, v => v),
        County = Edit(x.County, v => v),
        TowingCapacityKilograms = Edit(x.TowingCapacityKilograms, v => v),
        InspectionValidThrough = Edit(x.InspectionValidThrough, v => v),
        ServiceDocumentation = Edit(x.ServiceDocumentation, v => (C.ServiceDocumentationStatus)v),
        LastServiceDate = Edit(x.LastServiceDate, v => v),
        LastServiceOdometerKilometres = Edit(x.LastServiceOdometerKilometres, v => v),
        ServiceNotes = Edit(x.ServiceNotes, v => v),
        ConditionNotes = x.ConditionNotes?.Select(x => Edit(x, v => v)
            ?? throw S.ComparisonFactOperations.Error("conditionNotes", "required", "A note operation is required.")).ToArray(),
    };
    private static S.FactEdit<U>? Edit<T,U>(A.FactEdit<T>? x, Func<T,U> convert) where T : notnull where U : notnull => x is null ? null :
        new((S.FactEditKind)x.Kind, Manual(x.Manual, convert), x.Observations?.Select((o, i) => o is null
            ? throw S.ComparisonFactOperations.Error($"observations[{i}]", "required", "An observation selection is required.")
            : new S.FactSelection<U>((S.FactSelectionKind)o.Kind, o.ObservationIndex, Manual(o.Manual, convert))).ToArray());
    private static S.ManualFactValue<U>? Manual<T,U>(A.ManualFactValue<T>? x, Func<T,U> convert) where T : notnull where U : notnull =>
        x is null ? null : new(convert(x.Value), x.ObservedAt);

    public static C.RuleProfileInput ToCore(A.RuleProfileInput x) => new(
        x.HardRules.Select((r, i) => r is null ? throw S.ComparisonFactOperations.Error($"hardRules[{i}]", "required", "A rule is required.")
            : new C.HardRuleInput(r.CriterionKey, (C.HardRuleOperator)r.Operator, (C.EvidenceRequirement)r.MinimumEvidence,
                r.Minimum, r.Maximum, Choices(r.AllowedValues), r.Enabled)),
        x.Preferences.Select((p, i) => p is null ? throw S.ComparisonFactOperations.Error($"preferences[{i}]", "required", "A preference is required.")
            : new C.PreferenceInput(p.CriterionKey, p.Weight, (C.EvidenceRequirement)p.MinimumEvidence, p.ZeroPoint, p.FullPoint, Choices(p.PreferredValues))),
        x.Signals.Select((p, i) => p is null ? throw S.ComparisonFactOperations.Error($"signals[{i}]", "required", "A signal is required.")
            : new C.ComparisonSignalInput((C.ComparisonSignalKey)p.Key, p.ShortInspectionDays)));
    private static C.ComparisonChoice[]? Choices(IReadOnlyList<A.ComparisonChoice>? values) => values?.Select((x, i) => x is null
        ? throw S.ComparisonFactOperations.Error($"values[{i}]", "required", "A choice is required.") : new C.ComparisonChoice(x.Boolean,
            (L.Transmission?)x.Transmission, (L.FuelType?)x.FuelType, (L.BodyType?)x.BodyType, (L.Drivetrain?)x.Drivetrain,
            (C.ServiceDocumentationStatus?)x.ServiceDocumentation, x.Text)).ToArray();
    public static A.ComparisonChoice ToApi(C.ComparisonChoice x) => new()
    { Boolean = x.Boolean, Transmission = (AL.Transmission?)x.Transmission, FuelType = (AL.FuelType?)x.FuelType,
      BodyType = (AL.BodyType?)x.BodyType, Drivetrain = (AL.Drivetrain?)x.Drivetrain,
      ServiceDocumentation = (A.ServiceDocumentationStatus?)x.ServiceDocumentation, Text = x.Text };
    public static A.HardRuleInput ToApi(C.HardRuleInput x) => new()
    { CriterionKey = x.CriterionKey, Operator = (A.HardRuleOperator)x.Operator, MinimumEvidence = (A.EvidenceRequirement)x.MinimumEvidence,
      Minimum = x.Minimum, Maximum = x.Maximum, AllowedValues = x.AllowedValues?.Select(ToApi).ToArray(), Enabled = x.Enabled };
    public static A.PreferenceInput ToApi(C.PreferenceInput x) => new()
    { CriterionKey = x.CriterionKey, Weight = x.Weight, MinimumEvidence = (A.EvidenceRequirement)x.MinimumEvidence,
      ZeroPoint = x.ZeroPoint, FullPoint = x.FullPoint, PreferredValues = x.PreferredValues?.Select(ToApi).ToArray() };
    public static A.RuleProfileInput ToApi(C.RuleProfileInput x) => new()
    { HardRules = x.HardRules.Select(ToApi).ToArray(), Preferences = x.Preferences.Select(ToApi).ToArray(),
      Signals = x.Signals.Select(v => new A.ComparisonSignalInput { Key = (A.ComparisonSignalKey)v.Key, ShortInspectionDays = v.ShortInspectionDays }).ToArray() };
    public static A.ComparisonEvidence ToApi(C.ComparisonEvidence x) => new((AL.FieldOrigin)x.Origin,
        (AL.ExtractionMethod)x.ExtractionMethod, (AL.VerificationStatus)x.Verification, x.SourceUrl?.Value, x.ObservedAt, x.ConfirmedAt);
    public static A.ComparisonFactSet ToApi(S.ComparisonFactSet x)
    {
        A.VehicleFact<U> Fact<T,U>(string key, C.VehicleFact<T>? fact, Func<T,U> convert) where T : notnull
        {
            fact ??= C.VehicleFact<T>.Unknown();
            var versions = x.ObservationListingVersions.GetValueOrDefault(key);
            return new((A.VehicleFactState)fact.State, fact.Observations.Select((v, i) =>
                new A.FactObservation<U>(convert(v.Value), ToApi(v.Evidence), versions?.ElementAtOrDefault(i))).ToArray());
        }
        return new(new()
        {
            PurchasePriceSek = Fact("purchasePriceSek", x.Facts.PurchasePriceSek, v => v),
            OdometerKilometres = Fact("odometerKilometres", x.Facts.OdometerKilometres, v => v),
            OwnerCount = Fact("ownerCount", x.Facts.OwnerCount, v => v),
            TowBar = Fact("towBar", x.Facts.TowBar, v => v),
            Transmission = Fact("transmission", x.Facts.Transmission, v => (AL.Transmission)v),
            Seats = Fact("seats", x.Facts.Seats, v => v),
            ModelYear = Fact("modelYear", x.Facts.ModelYear, v => v),
            FuelTypes = Fact("fuelTypes", x.Facts.FuelTypes, v => (IReadOnlyList<AL.FuelType>)v.Values.Select(x => (AL.FuelType)x).ToArray()),
            BodyType = Fact("bodyType", x.Facts.BodyType, v => (AL.BodyType)v),
            Drivetrain = Fact("drivetrain", x.Facts.Drivetrain, v => (AL.Drivetrain)v),
            Locality = Fact("locality", x.Facts.Locality, v => v),
            County = Fact("county", x.Facts.County, v => v),
            TowingCapacityKilograms = Fact("towingCapacityKilograms", x.Facts.TowingCapacityKilograms, v => v),
            InspectionValidThrough = Fact("inspectionValidThrough", x.Facts.InspectionValidThrough, v => v),
            ServiceDocumentation = Fact("serviceDocumentation", x.Facts.ServiceDocumentation, v => (A.ServiceDocumentationStatus)v),
            LastServiceDate = Fact("lastServiceDate", x.Facts.LastServiceDate, v => v),
            LastServiceOdometerKilometres = Fact("lastServiceOdometerKilometres", x.Facts.LastServiceOdometerKilometres, v => v),
            ServiceNotes = Fact("serviceNotes", x.Facts.ServiceNotes, v => v),
        }, x.ConditionNotes?.Select((n, i) => Fact($"conditionNotes[{i}]", n, v => v)).ToArray());
    }
    public static A.VehicleFactsResponse ToApi(S.SavedVehicleFacts x) => new(x.VehicleId, x.RegistrationNumber.Value, x.Revision,
        x.Input is null ? null : ToApi(x.Input), x.ListingProposal is null ? null : ToApi(x.ListingProposal),
        x.CurrentListingVersion, x.FactsReviewedListingVersion, x.CostReviewedListingVersion, x.NeedsListingReview, x.CostConfirmedAt);

    private static readonly JsonSerializerOptions EqualityOptions = new() { Converters = { new CanonicalDecimalConverter() } };
    public static bool Equivalent<T>(T a, T b) => JsonSerializer.Serialize(a, EqualityOptions) == JsonSerializer.Serialize(b, EqualityOptions);
    private sealed class CanonicalDecimalConverter : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetDecimal();
        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
            writer.WriteRawValue(value.ToString("G29", CultureInfo.InvariantCulture));
    }
}
