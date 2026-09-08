using CarExpenseCalculator.Core.Comparisons;
using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Core.Households;
using CarExpenseCalculator.Infrastructure.Persistence.Comparisons;
using CarExpenseCalculator.Infrastructure.Persistence;
using CarExpenseCalculator.Infrastructure.Persistence.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace CarExpenseCalculator.Infrastructure.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class ComparisonStoreTests(PostgreSqlFixture fixture) : HouseholdTestSupport(fixture)
{
    private static VehicleFactsStore Facts(CarExpenseDbContext db) => new(db, TimeProvider.System);
    private static FactEdit<T> Manual<T>(T value) where T : notnull => new(FactEditKind.Manual, new(value));
    private static T Value<T>(VehicleFact<T>? fact) where T : notnull => Assert.Single(fact!.Observations).Value;

    [Fact]
    public async Task Profile_is_empty_until_saved_and_round_trips_rules_without_touching_household()
    {
        await Fixture.ResetDatabaseAsync();
        await using var db = Fixture.CreateDbContext();
        var store = new RuleProfileStore(db);
        Assert.Equal(new SavedRuleProfile(null, 0), await store.GetAsync());
        var rules = new RuleProfileInput([new("towBar", HardRuleOperator.Equals, EvidenceRequirement.UserConfirmed, allowedValues: [new(Boolean: true)])],
            [new("purchasePriceSek", 3, EvidenceRequirement.Advertised, 100000.1234567890123456789m, 20000)],
            [new(ComparisonSignalKey.InspectionValidity, 60)]);
        var saved = await store.SaveAsync(rules, 0);
        Assert.Equal(1, saved.Revision);
        Assert.Equal(100000.1234567890123456789m, Assert.Single(saved.Input!.Preferences).ZeroPoint);
        Assert.Equal(60, Assert.Single(saved.Input.Signals).ShortInspectionDays);
        Assert.Equal(new SavedHouseholdProfile(null, 0), await new HouseholdProfileStore(db).GetAsync());
        Assert.Empty((await store.SaveAsync(new(), 1)).Input!.HardRules);
        Assert.Equal(1, await CountAsync("rule_profile"));
        Assert.Equal("ruleProfileRevisionConflict", (await Assert.ThrowsAsync<ComparisonStoreException>(() => store.SaveAsync(new(), 1))).Code);
    }

    [Fact]
    public async Task Every_fact_field_round_trips_typed_values_sources_and_precision()
    {
        await Fixture.ResetDatabaseAsync();
        await using var db = Fixture.CreateDbContext();
        var vehicle = await Costs(db).CreateAsync(Reg(), Write());
        Assert.Null((await Facts(db).GetAsync(vehicle.VehicleId))!.Input);
        var edits = new VehicleFactEdits
        {
            PurchasePriceSek = Manual(123456.1234567890123456789012m), OdometerKilometres = Manual(200000.123456789012345678901m),
            OwnerCount = Manual(0), TowBar = Manual(false), Transmission = Manual(Transmission.Automatic), Seats = Manual(5),
            ModelYear = Manual(2020), FuelTypes = Manual(new FuelTypeSet([FuelType.Biogas, FuelType.Petrol])),
            BodyType = Manual(BodyType.Wagon), Drivetrain = Manual(Drivetrain.AllWheelDrive), Locality = Manual("  Go\u0308teborg "),
            County = Manual(" Västra Götaland "), TowingCapacityKilograms = Manual(0),
            InspectionValidThrough = Manual(new DateOnly(2027, 1, 1)), ServiceDocumentation = Manual(ServiceDocumentationStatus.Partial),
            LastServiceDate = Manual(new DateOnly(2026, 2, 1)), LastServiceOdometerKilometres = Manual(190000.1234567890123456789m),
            ServiceNotes = Manual(" Invoice retained "), ConditionNotes = [Manual("Seller reports a scratch")],
        };
        await Facts(db).SaveAsync(vehicle.VehicleId, 1, new(edits));
        await using var other = Fixture.CreateDbContext();
        var saved = (await Facts(other).GetAsync(vehicle.VehicleId))!;
        Assert.Equal(2, saved.Revision);
        var f = saved.Input!.Facts;
        Assert.Equal(123456.1234567890123456789012m, Value(f.PurchasePriceSek));
        Assert.Equal(200000.123456789012345678901m, Value(f.OdometerKilometres));
        Assert.Equal(0, Value(f.OwnerCount)); Assert.False(Value(f.TowBar));
        Assert.Equal(Transmission.Automatic, Value(f.Transmission)); Assert.Equal(5, Value(f.Seats)); Assert.Equal(2020, Value(f.ModelYear));
        Assert.Equal([FuelType.Biogas, FuelType.Petrol], Value(f.FuelTypes).Values);
        Assert.Equal(BodyType.Wagon, Value(f.BodyType)); Assert.Equal(Drivetrain.AllWheelDrive, Value(f.Drivetrain));
        Assert.Equal("Göteborg", Value(f.Locality)); Assert.Equal("Västra Götaland", Value(f.County));
        Assert.Equal(0, Value(f.TowingCapacityKilograms)); Assert.Equal(new DateOnly(2027, 1, 1), Value(f.InspectionValidThrough));
        Assert.Equal(ServiceDocumentationStatus.Partial, Value(f.ServiceDocumentation));
        Assert.Equal(new DateOnly(2026, 2, 1), Value(f.LastServiceDate));
        Assert.Equal(190000.1234567890123456789m, Value(f.LastServiceOdometerKilometres));
        Assert.Equal("Invoice retained", Value(f.ServiceNotes));
        Assert.Equal("Seller reports a scratch", Value(Assert.Single(saved.Input.ConditionNotes!)));
        Assert.Equal(VerificationStatus.UserConfirmed, f.Seats!.Observations[0].Evidence.Verification);
        Assert.Null(f.Seats.Observations[0].Evidence.SourceUrl); Assert.NotNull(f.Seats.Observations[0].Evidence.ConfirmedAt);
    }

    [Fact]
    public async Task Empty_unknown_not_applicable_conflicts_and_explicit_resolution_are_retained()
    {
        await Fixture.ResetDatabaseAsync();
        await using var db = Fixture.CreateDbContext();
        var vehicle = await Costs(db).CreateAsync(Reg(), Write());
        var saved = await Facts(db).SaveAsync(vehicle.VehicleId, 1, new(new()
        {
            FuelTypes = Manual(new FuelTypeSet([])), PurchasePriceSek = new(FactEditKind.NotApplicable),
            OwnerCount = new(FactEditKind.Conflict, Observations: [new(FactSelectionKind.Manual, Manual: new(2)), new(FactSelectionKind.Manual, Manual: new(3))]),
            ConditionNotes = [],
        }));
        Assert.Empty(Value(saved.Input!.Facts.FuelTypes).Values);
        Assert.Equal(VehicleFactState.Unknown, saved.Input.Facts.Seats!.State);
        Assert.Equal(VehicleFactState.NotApplicable, saved.Input.Facts.PurchasePriceSek!.State);
        Assert.Equal(VehicleFactState.Conflicting, saved.Input.Facts.OwnerCount!.State);
        Assert.Empty(saved.Input.ConditionNotes!);
        await Assert.ThrowsAsync<ComparisonInputValidationException>(() => Facts(db).SaveAsync(vehicle.VehicleId, 2, new(new() { OwnerCount = Manual(2) })));
        var resolved = await Facts(db).SaveAsync(vehicle.VehicleId, 2, new(new() { OwnerCount = new(FactEditKind.Resolve, new(4)) }));
        Assert.Equal(4, Value(resolved.Input!.Facts.OwnerCount));
        Assert.Equal(1, await CountAsync("vehicle_comparison_facts"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cost_confirmation_is_invalidated_by_changed_costs_including_draft_adoption(bool adopt)
    {
        await Fixture.ResetDatabaseAsync();
        await using var db = Fixture.CreateDbContext();
        var car = await Costs(db).CreateAsync(Reg(), Write(40000));
        var confirmed = await Facts(db).SaveAsync(car.VehicleId, 1, new(new(), CostConfirmation: CostConfirmationAction.Confirm));
        var stamp = confirmed.CostConfirmedAt;
        Assert.NotNull(stamp);
        await new HouseholdProfileStore(db).SaveAsync(Profile(), 0);
        await new RuleProfileStore(db).SaveAsync(new(), 0);
        await Costs(db).ReplaceAsync(car.VehicleId, 2, Write(40000.00m));
        Assert.Equal(stamp, (await Facts(db).GetAsync(car.VehicleId))!.CostConfirmedAt);
        await Facts(db).SaveAsync(car.VehicleId, 3, new(new() { Seats = Manual(5) }));
        Assert.Equal(stamp, (await Facts(db).GetAsync(car.VehicleId))!.CostConfirmedAt);
        if (adopt)
        {
            await Drafts(db).SaveAsync(new(Reg(), Write(35000), BaseVehicleId: car.VehicleId, BaseVehicleRevision: 4), 0);
            await Drafts(db).AdoptAsync(1);
        }
        else await Costs(db).ReplaceAsync(car.VehicleId, 4, Write(35000));
        Assert.Null((await Facts(db).GetAsync(car.VehicleId))!.CostConfirmedAt);
        await Costs(db).ReplaceAsync(car.VehicleId, 5, Write(40000));
        Assert.Null((await Facts(db).GetAsync(car.VehicleId))!.CostConfirmedAt);
    }

    [Fact]
    public async Task Listing_revisions_stay_attached_to_observations_until_explicit_adoption()
    {
        await Fixture.ResetDatabaseAsync();
        await using var db = Fixture.CreateDbContext();
        var car = await Listings(db).CreateAsync(Reg(), ListingFactory.ManualOnly());
        var preview = (await Facts(db).GetAsync(car.VehicleId))!;
        Assert.Null(preview.Input);
        var imported = await Facts(db).SaveAsync(car.VehicleId, 1, new(new() { PurchasePriceSek = new(FactEditKind.Listing) }, 1, true));
        var oldPrice = Value(imported.Input!.Facts.PurchasePriceSek);
        await Listings(db).ReplaceAsync(car.VehicleId, 2, ListingFactory.Complete());
        var changed = (await Facts(db).GetAsync(car.VehicleId))!;
        Assert.True(changed.NeedsListingReview);
        Assert.Equal(oldPrice, Value(changed.Input!.Facts.PurchasePriceSek));
        Assert.Equal(1, Assert.Single(changed.Input.ObservationListingVersions["purchasePriceSek"]));
        var reviewed = await Facts(db).SaveAsync(car.VehicleId, 3, new(new(), 2, true));
        Assert.False(reviewed.NeedsListingReview);
        Assert.Equal(1, Assert.Single(reviewed.Input!.ObservationListingVersions["purchasePriceSek"]));
        var stale = await Assert.ThrowsAsync<ComparisonStoreException>(() => Facts(db).SaveAsync(car.VehicleId, 4,
            new(new() { PurchasePriceSek = new(FactEditKind.Listing) }, 1)));
        Assert.Equal("listingVersionConflict", stale.Code);
    }

    [Theory]
    [InlineData("cost")]
    [InlineData("listing")]
    [InlineData("legacy")]
    public async Task All_deletion_routes_remove_facts_and_matching_draft_but_preserve_profiles(string route)
    {
        await Fixture.ResetDatabaseAsync();
        await using var db = Fixture.CreateDbContext();
        var car = await Costs(db).CreateAsync(Reg(), Write());
        var revision = car.Revision;
        if (route == "listing") revision = (await Listings(db).ReplaceAsync(car.VehicleId, revision, ListingFactory.ManualOnly())).Revision;
        await new HouseholdProfileStore(db).SaveAsync(Profile(), 0);
        await new RuleProfileStore(db).SaveAsync(new(), 0);
        revision = (await Facts(db).SaveAsync(car.VehicleId, revision, new(new() { Seats = Manual(5) }, CostConfirmation: CostConfirmationAction.Confirm))).Revision;
        await Drafts(db).SaveAsync(new(Reg(), Write(), BaseVehicleId: car.VehicleId, BaseVehicleRevision: revision), 0);
        switch (route)
        {
            case "cost": await Costs(db).DeleteAsync(car.VehicleId, revision); break;
            case "listing": await Listings(db).DeleteAsync(car.VehicleId, revision); break;
            default: await Legacy(db).DeleteAsync(car.VehicleId, revision); break;
        }
        Assert.Equal(0, await CountAsync("vehicle_comparison_facts"));
        Assert.Null((await Drafts(db).GetAsync()).Input); Assert.Equal(2, (await Drafts(db).GetAsync()).Revision);
        Assert.Equal(1, (await new HouseholdProfileStore(db).GetAsync()).Revision);
        Assert.Equal(1, (await new RuleProfileStore(db).GetAsync()).Revision);
        await Assert.ThrowsAsync<ComparisonStoreException>(() => Facts(db).SaveAsync(car.VehicleId, 2, new(new())));
    }

    [Fact]
    public async Task Rollback_preserves_existing_data_and_reapply_starts_empty()
    {
        await Fixture.ResetDatabaseAsync();
        await using var db = Fixture.CreateDbContext();
        var car = await Costs(db).CreateAsync(Reg(), Write());
        await new HouseholdProfileStore(db).SaveAsync(Profile(), 0);
        await new RuleProfileStore(db).SaveAsync(new(), 0);
        await Facts(db).SaveAsync(car.VehicleId, 1, new(new(), CostConfirmation: CostConfirmationAction.Confirm));
        var migrator = db.Database.GetService<IMigrator>();
        await migrator.MigrateAsync("20260906151351_AddHouseholdPersistence");
        Assert.Equal(1, await CountAsync("vehicles")); Assert.Equal(1, await CountAsync("vehicle_cost_inputs"));
        Assert.Equal(1, await ScalarAsync<long>("SELECT profile_revision FROM household_state"));
        Assert.False(await ScalarAsync<bool>("SELECT to_regclass('public.rule_profile') IS NOT NULL"));
        await migrator.MigrateAsync();
        Assert.Null((await Facts(db).GetAsync(car.VehicleId))!.Input);
        Assert.Equal(new SavedRuleProfile(null, 0), await new RuleProfileStore(db).GetAsync());
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
