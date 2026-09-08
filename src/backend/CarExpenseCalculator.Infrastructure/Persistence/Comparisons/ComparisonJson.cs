using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CarExpenseCalculator.Core.Comparisons;
using CarExpenseCalculator.Core.Households;
using CarExpenseCalculator.Core.Listings;

namespace CarExpenseCalculator.Infrastructure.Persistence.Comparisons;

internal static class ComparisonJson
{
    public const int Version = 1;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false) },
    };
    public static string Serialize<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, Options);
        if (Encoding.UTF8.GetByteCount(json) > 2 * 1024 * 1024)
            throw new ComparisonStoreException("payloadTooLarge", "Stored comparison input exceeds 2 MiB.");
        return json;
    }
    public static T Read<T>(string json, int version)
    {
        if (version != Version) throw new ComparisonStoreException("unsupportedComparisonStorageVersion", "The comparison storage version is not supported.");
        return JsonSerializer.Deserialize<T>(json, Options) ?? throw new JsonException("Stored input is missing.");
    }
    public static T Decode<T>(Func<T> read)
    {
        try { return read(); }
        catch (Exception e) when (e is JsonException or ArgumentException or NullReferenceException
            or VehicleFactsValidationException or ComparisonInputValidationException or IndexOutOfRangeException or InvalidOperationException)
        { throw new ComparisonStoreException("comparisonStorageUnavailable", "Stored comparison input cannot be read."); }
    }

    internal sealed record EvidencePayload(FieldOrigin Origin, ExtractionMethod ExtractionMethod, VerificationStatus Verification,
        string? SourceUrl, DateTimeOffset? ObservedAt, DateTimeOffset? ConfirmedAt)
    {
        public static EvidencePayload From(ComparisonEvidence x) => new(x.Origin, x.ExtractionMethod, x.Verification, x.SourceUrl?.Value, x.ObservedAt, x.ConfirmedAt);
        public ComparisonEvidence ToCore() => new(Origin, ExtractionMethod, Verification, SourceUrl is null ? null : ListingUrl.Parse(SourceUrl), ObservedAt, ConfirmedAt);
    }
    internal sealed record ObservationPayload<T>(T Value, EvidencePayload Evidence, long? ListingVersion) where T : notnull;
    internal sealed record FactPayload<T>(VehicleFactState State, ObservationPayload<T>[] Observations) where T : notnull
    {
        public static FactPayload<T>? From<U>(VehicleFact<U>? x, IReadOnlyList<long?>? versions, Func<U, T> convert) where U : notnull =>
            x is null ? null : new(x.State, x.Observations.Select((o, i) => new ObservationPayload<T>(convert(o.Value),
                EvidencePayload.From(o.Evidence), versions?.ElementAtOrDefault(i))).ToArray());
        public VehicleFact<U> ToCore<U>(Func<T, U> convert) where U : notnull =>
            new(State, Observations.Select(x => new FactObservation<U>(convert(x.Value), x.Evidence.ToCore())));
        public IReadOnlyList<long?> Versions()
        {
            if (Observations.Any(x => x.ListingVersion is <= 0)) throw new JsonException("Invalid source listing version.");
            return Array.AsReadOnly(Observations.Select(x => x.ListingVersion).ToArray());
        }
    }
    internal sealed record FactsPayload(
        FactPayload<decimal>? PurchasePriceSek,
        FactPayload<decimal>? OdometerKilometres,
        FactPayload<int>? OwnerCount,
        FactPayload<bool>? TowBar,
        FactPayload<Transmission>? Transmission,
        FactPayload<int>? Seats,
        FactPayload<int>? ModelYear,
        FactPayload<FuelType[]>? FuelTypes,
        FactPayload<BodyType>? BodyType,
        FactPayload<Drivetrain>? Drivetrain,
        FactPayload<string>? Locality,
        FactPayload<string>? County,
        FactPayload<int>? TowingCapacityKilograms,
        FactPayload<DateOnly>? InspectionValidThrough,
        FactPayload<ServiceDocumentationStatus>? ServiceDocumentation,
        FactPayload<DateOnly>? LastServiceDate,
        FactPayload<decimal>? LastServiceOdometerKilometres,
        FactPayload<string>? ServiceNotes,
        FactPayload<string>[]? ConditionNotes)
    {
        public static FactsPayload From(ComparisonFactSet x) => new(
            FactPayload<decimal>.From(x.Facts.PurchasePriceSek, x.ObservationListingVersions.GetValueOrDefault("purchasePriceSek"), v => v),
            FactPayload<decimal>.From(x.Facts.OdometerKilometres, x.ObservationListingVersions.GetValueOrDefault("odometerKilometres"), v => v),
            FactPayload<int>.From(x.Facts.OwnerCount, x.ObservationListingVersions.GetValueOrDefault("ownerCount"), v => v),
            FactPayload<bool>.From(x.Facts.TowBar, x.ObservationListingVersions.GetValueOrDefault("towBar"), v => v),
            FactPayload<Transmission>.From(x.Facts.Transmission, x.ObservationListingVersions.GetValueOrDefault("transmission"), v => v),
            FactPayload<int>.From(x.Facts.Seats, x.ObservationListingVersions.GetValueOrDefault("seats"), v => v),
            FactPayload<int>.From(x.Facts.ModelYear, x.ObservationListingVersions.GetValueOrDefault("modelYear"), v => v),
            FactPayload<FuelType[]>.From(x.Facts.FuelTypes, x.ObservationListingVersions.GetValueOrDefault("fuelTypes"), v => v.Values.ToArray()),
            FactPayload<BodyType>.From(x.Facts.BodyType, x.ObservationListingVersions.GetValueOrDefault("bodyType"), v => v),
            FactPayload<Drivetrain>.From(x.Facts.Drivetrain, x.ObservationListingVersions.GetValueOrDefault("drivetrain"), v => v),
            FactPayload<string>.From(x.Facts.Locality, x.ObservationListingVersions.GetValueOrDefault("locality"), v => v),
            FactPayload<string>.From(x.Facts.County, x.ObservationListingVersions.GetValueOrDefault("county"), v => v),
            FactPayload<int>.From(x.Facts.TowingCapacityKilograms, x.ObservationListingVersions.GetValueOrDefault("towingCapacityKilograms"), v => v),
            FactPayload<DateOnly>.From(x.Facts.InspectionValidThrough, x.ObservationListingVersions.GetValueOrDefault("inspectionValidThrough"), v => v),
            FactPayload<ServiceDocumentationStatus>.From(x.Facts.ServiceDocumentation, x.ObservationListingVersions.GetValueOrDefault("serviceDocumentation"), v => v),
            FactPayload<DateOnly>.From(x.Facts.LastServiceDate, x.ObservationListingVersions.GetValueOrDefault("lastServiceDate"), v => v),
            FactPayload<decimal>.From(x.Facts.LastServiceOdometerKilometres, x.ObservationListingVersions.GetValueOrDefault("lastServiceOdometerKilometres"), v => v),
            FactPayload<string>.From(x.Facts.ServiceNotes, x.ObservationListingVersions.GetValueOrDefault("serviceNotes"), v => v),
            x.ConditionNotes?.Select((note, i) => FactPayload<string>.From(note,
                x.ObservationListingVersions.GetValueOrDefault($"conditionNotes[{i}]"), v => v)!).ToArray());
        public ComparisonFactSet ToInput()
        {
            var versions = new Dictionary<string, IReadOnlyList<long?>>(StringComparer.Ordinal);
            if (PurchasePriceSek is not null) versions["purchasePriceSek"] = PurchasePriceSek.Versions();
            if (OdometerKilometres is not null) versions["odometerKilometres"] = OdometerKilometres.Versions();
            if (OwnerCount is not null) versions["ownerCount"] = OwnerCount.Versions();
            if (TowBar is not null) versions["towBar"] = TowBar.Versions();
            if (Transmission is not null) versions["transmission"] = Transmission.Versions();
            if (Seats is not null) versions["seats"] = Seats.Versions();
            if (ModelYear is not null) versions["modelYear"] = ModelYear.Versions();
            if (FuelTypes is not null) versions["fuelTypes"] = FuelTypes.Versions();
            if (BodyType is not null) versions["bodyType"] = BodyType.Versions();
            if (Drivetrain is not null) versions["drivetrain"] = Drivetrain.Versions();
            if (Locality is not null) versions["locality"] = Locality.Versions();
            if (County is not null) versions["county"] = County.Versions();
            if (TowingCapacityKilograms is not null) versions["towingCapacityKilograms"] = TowingCapacityKilograms.Versions();
            if (InspectionValidThrough is not null) versions["inspectionValidThrough"] = InspectionValidThrough.Versions();
            if (ServiceDocumentation is not null) versions["serviceDocumentation"] = ServiceDocumentation.Versions();
            if (LastServiceDate is not null) versions["lastServiceDate"] = LastServiceDate.Versions();
            if (LastServiceOdometerKilometres is not null) versions["lastServiceOdometerKilometres"] = LastServiceOdometerKilometres.Versions();
            if (ServiceNotes is not null) versions["serviceNotes"] = ServiceNotes.Versions();
            if (ConditionNotes is not null)
                for (var i = 0; i < ConditionNotes.Length; i++) versions[$"conditionNotes[{i}]"] = ConditionNotes[i].Versions();
            return new(new()
            {
                PurchasePriceSek = PurchasePriceSek?.ToCore(v => v),
                OdometerKilometres = OdometerKilometres?.ToCore(v => v),
                OwnerCount = OwnerCount?.ToCore(v => v),
                TowBar = TowBar?.ToCore(v => v),
                Transmission = Transmission?.ToCore(v => v),
                Seats = Seats?.ToCore(v => v),
                ModelYear = ModelYear?.ToCore(v => v),
                FuelTypes = FuelTypes?.ToCore(v => new FuelTypeSet(v)),
                BodyType = BodyType?.ToCore(v => v),
                Drivetrain = Drivetrain?.ToCore(v => v),
                Locality = Locality?.ToCore(v => v),
                County = County?.ToCore(v => v),
                TowingCapacityKilograms = TowingCapacityKilograms?.ToCore(v => v),
                InspectionValidThrough = InspectionValidThrough?.ToCore(v => v),
                ServiceDocumentation = ServiceDocumentation?.ToCore(v => v),
                LastServiceDate = LastServiceDate?.ToCore(v => v),
                LastServiceOdometerKilometres = LastServiceOdometerKilometres?.ToCore(v => v),
                ServiceNotes = ServiceNotes?.ToCore(v => v),
            }, versions, ConditionNotes?.Select(x => x.ToCore(v => v)));
        }
    }

    internal sealed record ChoicePayload(bool? Boolean, Transmission? Transmission, FuelType? FuelType, BodyType? BodyType,
        Drivetrain? Drivetrain, ServiceDocumentationStatus? ServiceDocumentation, string? Text)
    {
        public static ChoicePayload From(ComparisonChoice x) => new(x.Boolean, x.Transmission, x.FuelType, x.BodyType, x.Drivetrain, x.ServiceDocumentation, x.Text);
        public ComparisonChoice ToCore() => new(Boolean, Transmission, FuelType, BodyType, Drivetrain, ServiceDocumentation, Text);
    }
    internal sealed record HardPayload(string CriterionKey, HardRuleOperator Operator, EvidenceRequirement MinimumEvidence,
        decimal? Minimum, decimal? Maximum, ChoicePayload[]? AllowedValues, bool Enabled)
    {
        public static HardPayload From(HardRuleInput x) => new(x.CriterionKey, x.Operator, x.MinimumEvidence, x.Minimum, x.Maximum, x.AllowedValues?.Select(ChoicePayload.From).ToArray(), x.Enabled);
        public HardRuleInput ToCore() => new(CriterionKey, Operator, MinimumEvidence, Minimum, Maximum, AllowedValues?.Select(x => x.ToCore()), Enabled);
    }
    internal sealed record PreferencePayload(string CriterionKey, int Weight, EvidenceRequirement MinimumEvidence,
        decimal? ZeroPoint, decimal? FullPoint, ChoicePayload[]? PreferredValues)
    {
        public static PreferencePayload From(PreferenceInput x) => new(x.CriterionKey, x.Weight, x.MinimumEvidence, x.ZeroPoint, x.FullPoint, x.PreferredValues?.Select(ChoicePayload.From).ToArray());
        public PreferenceInput ToCore() => new(CriterionKey, Weight, MinimumEvidence, ZeroPoint, FullPoint, PreferredValues?.Select(x => x.ToCore()));
    }
    internal sealed record SignalPayload(ComparisonSignalKey Key, int? ShortInspectionDays);
    internal sealed record RulePayload(HardPayload[] HardRules, PreferencePayload[] Preferences, SignalPayload[] Signals)
    {
        public static RulePayload From(RuleProfileInput x) => new(x.HardRules.Select(HardPayload.From).ToArray(),
            x.Preferences.Select(PreferencePayload.From).ToArray(), x.Signals.Select(v => new SignalPayload(v.Key, v.ShortInspectionDays)).ToArray());
        public RuleProfileInput ToCore() => new(HardRules.Select(x => x.ToCore()), Preferences.Select(x => x.ToCore()),
            Signals.Select(x => new ComparisonSignalInput(x.Key, x.ShortInspectionDays)));
    }
}
