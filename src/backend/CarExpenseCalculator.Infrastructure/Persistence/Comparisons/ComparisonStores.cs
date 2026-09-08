using CarExpenseCalculator.Core.Comparisons;
using CarExpenseCalculator.Core.Households;
using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Core.Vehicles;
using CarExpenseCalculator.Infrastructure.Persistence.Households;
using CarExpenseCalculator.Infrastructure.Persistence.SavedListings;
using CarExpenseCalculator.Infrastructure.Persistence.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace CarExpenseCalculator.Infrastructure.Persistence.Comparisons;

public sealed class RuleProfileStore(CarExpenseDbContext db) : IRuleProfileStore
{
    public async Task<SavedRuleProfile> GetAsync(CancellationToken cancellationToken = default) =>
        Read(await db.Set<RuleProfileEntity>().AsNoTracking().SingleAsync(cancellationToken));
    public async Task<SavedRuleProfile> SaveAsync(RuleProfileInput input, long expectedRevision, CancellationToken cancellationToken = default)
    {
        var normalized = new RuleProfileProcessor().Normalize(input);
        var json = ComparisonJson.Serialize(ComparisonJson.RulePayload.From(normalized));
        await using var session = await HouseholdWriteSession.BeginAsync(db, cancellationToken);
        var row = await db.Set<RuleProfileEntity>().SingleAsync(cancellationToken);
        VehicleFactsStore.Revision(expectedRevision, row.Revision, "ruleProfileRevisionConflict");
        _ = Read(row); // An unsupported/corrupt version cannot be overwritten silently.
        row.Revision = checked(row.Revision + 1); row.InputJson = json; row.SchemaVersion = ComparisonJson.Version;
        var result = Read(row);
        await session.CommitAsync(cancellationToken);
        return result;
    }
    internal static SavedRuleProfile Read(RuleProfileEntity row)
    {
        if (row.SchemaVersion != ComparisonJson.Version) throw new ComparisonStoreException("unsupportedComparisonStorageVersion", "Unsupported rule-profile storage version.");
        return ComparisonJson.Decode(() => new SavedRuleProfile(row.InputJson is null ? null
            : new RuleProfileProcessor().Normalize(ComparisonJson.Read<ComparisonJson.RulePayload>(row.InputJson, row.SchemaVersion).ToCore()), row.Revision));
    }
}

public sealed class VehicleFactsStore(CarExpenseDbContext db, TimeProvider timeProvider) : IVehicleFactsStore
{
    public Task<SavedVehicleFacts?> GetAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
        HouseholdStoreData.ReadAsync(db, async () =>
        {
            var vehicle = await HouseholdStoreData.Vehicles(db, false).SingleOrDefaultAsync(x => x.Id == vehicleId, cancellationToken);
            return vehicle is null ? null : Read(vehicle);
        }, cancellationToken);

    public async Task<SavedVehicleFacts> SaveAsync(Guid vehicleId, long expectedRevision, VehicleFactsWrite write, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(write);
        await using var session = await HouseholdWriteSession.BeginAsync(db, cancellationToken);
        var vehicle = await HouseholdStoreData.Vehicles(db).SingleOrDefaultAsync(x => x.Id == vehicleId, cancellationToken)
            ?? throw new ComparisonStoreException("vehicleNotFound", "Vehicle no longer exists.", vehicleId);
        Revision(expectedRevision, vehicle.Revision, "vehicleRevisionConflict", vehicleId);
        var saved = Read(vehicle);
        CheckListing(write, saved);
        var now = ComparisonFactOperations.OperationTime(timeProvider);
        var input = ComparisonFactOperations.Normalize(ComparisonFactOperations.Apply(saved.Input, write.Edits,
            saved.ListingProposal, saved.CurrentListingVersion, now), CostInput(vehicle)?.AcquisitionType ?? AcquisitionType.Purchase);
        var json = ComparisonJson.Serialize(ComparisonJson.FactsPayload.From(input));
        var confirmedAt = Confirmation(write.CostConfirmation, saved.CostConfirmedAt, CostInput(vehicle), now);
        var row = vehicle.ComparisonFacts ??= new() { VehicleId = vehicle.Id, Vehicle = vehicle, InputJson = json };
        row.InputJson = json; row.SchemaVersion = ComparisonJson.Version; row.CostConfirmedAt = confirmedAt;
        if (write.ReviewCurrentListing) row.ReviewedListingVersion = saved.CurrentListingVersion;
        vehicle.Revision = checked(vehicle.Revision + 1); vehicle.UpdatedAtUtc = now;
        var result = Read(vehicle);
        await session.CommitAsync(cancellationToken);
        return result;
    }

    public static void CheckListing(VehicleFactsWrite write, SavedVehicleFacts saved)
    {
        if (write.ExpectedListingVersion is not null && write.ExpectedListingVersion != saved.CurrentListingVersion)
            throw new ComparisonStoreException("listingVersionConflict", "Listing version has changed.", saved.VehicleId,
                write.ExpectedListingVersion, saved.CurrentListingVersion);
        // Any listing operation must be explicit about the source version it reviewed.
        if ((UsesListing(write.Edits) || write.ReviewCurrentListing) &&
            (write.ExpectedListingVersion is null || saved.CurrentListingVersion is null))
            throw ComparisonFactOperations.Error("expectedListingVersion", "listingVersionRequired", "Listing adoption requires the current listing version.");
    }
    public static bool UsesListing(VehicleFactEdits edits)
    {
        bool Uses<T>(FactEdit<T>? e) where T : notnull => e?.Kind == FactEditKind.Listing ||
            e?.Observations?.Any(x => x?.Kind == FactSelectionKind.Listing) == true;
        return Uses(edits.PurchasePriceSek) ||
            Uses(edits.OdometerKilometres) ||
            Uses(edits.OwnerCount) ||
            Uses(edits.TowBar) ||
            Uses(edits.Transmission) ||
            Uses(edits.Seats) ||
            Uses(edits.ModelYear) ||
            Uses(edits.FuelTypes) ||
            Uses(edits.BodyType) ||
            Uses(edits.Drivetrain) ||
            Uses(edits.Locality) ||
            Uses(edits.County) ||
            Uses(edits.TowingCapacityKilograms) ||
            Uses(edits.InspectionValidThrough) ||
            Uses(edits.ServiceDocumentation) ||
            Uses(edits.LastServiceDate) ||
            Uses(edits.LastServiceOdometerKilometres) ||
            Uses(edits.ServiceNotes) ||
            edits.ConditionNotes?.Any(Uses) == true;
    }
    public static DateTimeOffset? Confirmation(CostConfirmationAction action, DateTimeOffset? previous, VehicleCostInput? input, DateTimeOffset now) => action switch
    {
        CostConfirmationAction.Preserve => previous,
        CostConfirmationAction.Clear => null,
        CostConfirmationAction.Confirm when input is not null => now,
        CostConfirmationAction.Confirm => throw ComparisonFactOperations.Error("costConfirmation", "costInputRequired", "Cost confirmation requires current car inputs."),
        _ => throw ComparisonFactOperations.Error("costConfirmation", "invalidEnum", "Unsupported confirmation action."),
    };
    internal static VehicleCostInput? CostInput(VehicleEntity vehicle) => vehicle.HouseholdCostInput is not { } cost ? null :
        HouseholdJson.Decode(() => HouseholdJson.Deserialize<HouseholdJson.StoredCostPayload>(cost.InputJson, cost.SchemaVersion).Input.ToCore());
    internal static SavedVehicleFacts Read(VehicleEntity vehicle)
        => ComparisonJson.Decode(() => ReadCurrent(vehicle));

    private static SavedVehicleFacts ReadCurrent(VehicleEntity vehicle)
    {
        var input = vehicle.ComparisonFacts is not { } stored ? null : ComparisonJson.Decode(() =>
            ComparisonFactOperations.Normalize(ComparisonJson.Read<ComparisonJson.FactsPayload>(stored.InputJson, stored.SchemaVersion).ToInput(),
                CostInput(vehicle)?.AcquisitionType ?? AcquisitionType.Purchase));
        ComparisonFactSet? proposal = null;
        if (vehicle.Listing is not null)
        {
            var listing = SavedListingStore.ToSavedListing(vehicle);
            var facts = new VehicleFactsProcessor().FromReviewedListing(ListingUrl.Parse(listing.SubmittedUrl),
                listing.ProcessingResult.Sources.Select(x => x.Url), listing.ProcessingResult.Listing,
                CostInput(vehicle)?.AcquisitionType ?? AcquisitionType.Purchase);
            var versions = new Dictionary<string, IReadOnlyList<long?>>(StringComparer.Ordinal);
            versions["purchasePriceSek"] = Array.AsReadOnly(Enumerable.Repeat<long?>(listing.ListingVersion, facts.PurchasePriceSek!.Observations.Count).ToArray());
            versions["odometerKilometres"] = Array.AsReadOnly(Enumerable.Repeat<long?>(listing.ListingVersion, facts.OdometerKilometres!.Observations.Count).ToArray());
            versions["ownerCount"] = Array.AsReadOnly(Enumerable.Repeat<long?>(listing.ListingVersion, facts.OwnerCount!.Observations.Count).ToArray());
            versions["towBar"] = Array.AsReadOnly(Enumerable.Repeat<long?>(listing.ListingVersion, facts.TowBar!.Observations.Count).ToArray());
            versions["transmission"] = Array.AsReadOnly(Enumerable.Repeat<long?>(listing.ListingVersion, facts.Transmission!.Observations.Count).ToArray());
            versions["seats"] = Array.AsReadOnly(Enumerable.Repeat<long?>(listing.ListingVersion, facts.Seats!.Observations.Count).ToArray());
            versions["modelYear"] = Array.AsReadOnly(Enumerable.Repeat<long?>(listing.ListingVersion, facts.ModelYear!.Observations.Count).ToArray());
            versions["fuelTypes"] = Array.AsReadOnly(Enumerable.Repeat<long?>(listing.ListingVersion, facts.FuelTypes!.Observations.Count).ToArray());
            versions["bodyType"] = Array.AsReadOnly(Enumerable.Repeat<long?>(listing.ListingVersion, facts.BodyType!.Observations.Count).ToArray());
            versions["drivetrain"] = Array.AsReadOnly(Enumerable.Repeat<long?>(listing.ListingVersion, facts.Drivetrain!.Observations.Count).ToArray());
            versions["locality"] = Array.AsReadOnly(Enumerable.Repeat<long?>(listing.ListingVersion, facts.Locality!.Observations.Count).ToArray());
            versions["county"] = Array.AsReadOnly(Enumerable.Repeat<long?>(listing.ListingVersion, facts.County!.Observations.Count).ToArray());
            versions["towingCapacityKilograms"] = Array.AsReadOnly(Enumerable.Repeat<long?>(listing.ListingVersion, facts.TowingCapacityKilograms!.Observations.Count).ToArray());
            versions["inspectionValidThrough"] = Array.AsReadOnly(Enumerable.Repeat<long?>(listing.ListingVersion, facts.InspectionValidThrough!.Observations.Count).ToArray());
            versions["serviceDocumentation"] = Array.AsReadOnly(Enumerable.Repeat<long?>(listing.ListingVersion, facts.ServiceDocumentation!.Observations.Count).ToArray());
            versions["lastServiceDate"] = Array.AsReadOnly(Enumerable.Repeat<long?>(listing.ListingVersion, facts.LastServiceDate!.Observations.Count).ToArray());
            versions["lastServiceOdometerKilometres"] = Array.AsReadOnly(Enumerable.Repeat<long?>(listing.ListingVersion, facts.LastServiceOdometerKilometres!.Observations.Count).ToArray());
            versions["serviceNotes"] = Array.AsReadOnly(Enumerable.Repeat<long?>(listing.ListingVersion, facts.ServiceNotes!.Observations.Count).ToArray());
            var sourcedNotes = listing.ProcessingResult.Listing.ConditionNotes;
            VehicleFact<string>[]? notes = null;
            if (sourcedNotes is not null)
            {
                var p = sourcedNotes.Provenance;
                notes = sourcedNotes.Values.Select(text => VehicleFact<string>.Known(text,
                    new(p.Origin, p.ExtractionMethod, p.Verification, p.SourceUrl))).ToArray();
                for (var i = 0; i < notes.Length; i++) versions[$"conditionNotes[{i}]"] = Array.AsReadOnly(new long?[] { listing.ListingVersion });
            }
            proposal = ComparisonFactOperations.Normalize(new(facts, versions, notes), CostInput(vehicle)?.AcquisitionType ?? AcquisitionType.Purchase);
        }
        return new(vehicle.Id, RegistrationNumber.Parse(vehicle.RegistrationNumber), vehicle.Revision, input, proposal,
            vehicle.Listing?.ListingVersion, vehicle.ComparisonFacts?.ReviewedListingVersion,
            vehicle.HouseholdCostInput?.SourceListingVersion ?? vehicle.Scenario?.SourceListingVersion,
            vehicle.ComparisonFacts?.CostConfirmedAt);
    }
    public static void Revision(long expected, long actual, string code, Guid? vehicleId = null)
    {
        if (expected < 0) throw ComparisonFactOperations.Error("expectedRevision", "outOfRange", "Revision cannot be negative.");
        if (expected != actual) throw new ComparisonStoreException(code, "Data changed since it was read.", vehicleId, expected, actual);
    }
}

public sealed class ComparisonSnapshotStore(CarExpenseDbContext db) : IComparisonSnapshotStore
{
    public Task<ComparisonSnapshot> ReadAsync(IReadOnlyList<Guid> vehicleIds, CancellationToken cancellationToken = default) =>
        HouseholdStoreData.ReadAsync(db, async () =>
        {
            var profile = HouseholdStoreData.Profile(await db.Set<HouseholdStateEntity>().AsNoTracking().SingleAsync(cancellationToken));
            var rules = RuleProfileStore.Read(await db.Set<RuleProfileEntity>().AsNoTracking().SingleAsync(cancellationToken));
            var vehicles = await HouseholdStoreData.Vehicles(db, false).Where(x => vehicleIds.Contains(x.Id)).ToListAsync(cancellationToken);
            var byId = vehicles.ToDictionary(x => x.Id);
            var result = vehicleIds.Select(id => byId.TryGetValue(id, out var vehicle)
                ? new ComparisonStoredVehicle(VehicleFactsStore.Read(vehicle), HouseholdStoreData.Vehicle(vehicle))
                : throw new ComparisonStoreException("vehicleNotFound", "Vehicle no longer exists.", id)).ToArray();
            return new ComparisonSnapshot(profile, rules, Array.AsReadOnly(result));
        }, cancellationToken);
}
