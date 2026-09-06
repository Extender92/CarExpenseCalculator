using CarExpenseCalculator.Core.Households;
using CarExpenseCalculator.Infrastructure.Persistence.Households;
using CarExpenseCalculator.Infrastructure.Persistence.SavedCostScenarios;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CarExpenseCalculator.Infrastructure.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class SharedVehicleDraftStoreTests(PostgreSqlFixture fixture) : HouseholdTestSupport(fixture)
{
    [Fact]
    public async Task One_registered_draft_survives_reads_requires_explicit_replacement_and_keeps_empty_revision()
    {
        await Fixture.ResetDatabaseAsync();
        await using var first = Fixture.CreateDbContext();
        await using var second = Fixture.CreateDbContext();
        Assert.Equal(new SavedVehicleDraft(0, null), await Drafts(first).GetAsync());
        await ErrorAsync("invalidDraft", () => Drafts(first).SaveAsync(new(Reg()), 0));
        var saved = await Drafts(first).SaveAsync(new(Reg(), Write(null)), 0);
        Assert.Equal(1, saved.Revision);
        Assert.NotNull((await Drafts(second).GetAsync()).Input);
        Assert.NotNull((await Drafts(second).GetAsync()).Input);
        await ErrorAsync("draftReplacementRequired", () => Drafts(second).SaveAsync(new(Reg("DEF456"), Write()), 1));
        await ErrorAsync("draftRevisionConflict", () => Drafts(second).SaveAsync(new(Reg("DEF456"), Write()), 0, true));
        await Drafts(second).SaveAsync(new(Reg("DEF456"), Write()), 1, true);
        Assert.Equal("DEF456", (await Drafts(first).GetAsync()).Input!.RegistrationNumber.Value);
        Assert.Equal(new SavedVehicleDraft(3, null), await Drafts(first).DeleteAsync(2));
        await ErrorAsync("draftRevisionConflict", () => Drafts(second).SaveAsync(new(Reg(), Write()), 1, true));
        Assert.Equal(new SavedVehicleDraft(4, null), await Drafts(first).DeleteAsync(3));
        await ErrorAsync("draftEmpty", () => Drafts(first).AdoptAsync(4));
        Assert.Equal(0, await CountAsync("vehicles"));
    }

    [Fact]
    public async Task Adoption_writes_both_parts_once_and_preserves_listing_provenance_and_precision()
    {
        await Fixture.ResetDatabaseAsync();
        await using var db = Fixture.CreateDbContext();
        var listing = ListingFactory.Complete();
        var cost = Write() with { ListingLinkMode = SavedScenarioListingLinkMode.Current };
        await Drafts(db).SaveAsync(new(Reg(), cost, listing), 0);
        await using var reader = Fixture.CreateDbContext();
        var reopened = (await Drafts(reader).GetAsync()).Input!;
        Assert.Equal(listing.Listing.PriceSek, reopened.Listing!.Listing.PriceSek);
        Assert.Equal(listing.Listing.PublishedDate, reopened.Listing.Listing.PublishedDate);
        Assert.Equal(listing.Listing.EnergyConsumptions!.Values.Select(x => x with { Label = x.Label.Trim() }), reopened.Listing.Listing.EnergyConsumptions!.Values);
        Assert.Equal(listing.Listing.FuelTypes!.Values, reopened.Listing.Listing.FuelTypes!.Values);
        var adopted = await Drafts(reader).AdoptAsync(1);
        Assert.Equal(1, adopted.Revision);
        Assert.Equal(1, adopted.SourceListingVersion);
        Assert.Equal(1, adopted.CurrentListingVersion);
        Assert.Equal(cost.Input.PriceSek, adopted.Input!.PriceSek);
        Assert.Equal(new SavedVehicleDraft(2, null), await Drafts(db).GetAsync());
        Assert.Equal(1, await CountAsync("vehicles"));
        Assert.Equal(1, await CountAsync("vehicle_cost_inputs"));
        Assert.Equal(1, await CountAsync("vehicle_listings"));
        var actualListing = (await Listings(db).GetAsync(adopted.VehicleId))!;
        Assert.Equal(listing.Listing.PriceSek!.Value, actualListing.ProcessingResult.Listing.PriceSek!.Value);
        Assert.Equal(listing.Listing.PriceSek.Provenance, actualListing.ProcessingResult.Listing.PriceSek.Provenance);
        Assert.Equal("Volvo V70", actualListing.ProcessingResult.Listing.VehicleLabel!.Value);
        Assert.Equal(listing.Listing.VehicleLabel!.Provenance, actualListing.ProcessingResult.Listing.VehicleLabel.Provenance);
        Assert.Null((await new HouseholdProfileStore(db).GetAsync()).Input);
    }

    [Theory]
    [InlineData("cost")]
    [InlineData("listing")]
    [InlineData("bothPreserve")]
    [InlineData("bothCurrent")]
    public async Task Partial_adoption_preserves_omitted_content_and_uses_explicit_listing_acknowledgment(string mode)
    {
        await Fixture.ResetDatabaseAsync();
        await using var db = Fixture.CreateDbContext();
        var listing = await Listings(db).CreateAsync(Reg(), ListingFactory.Complete());
        var current = await Costs(db).ReplaceAsync(listing.VehicleId, 1, Write() with { ListingLinkMode = SavedScenarioListingLinkMode.Current });
        var cost = mode == "listing" ? null : Write(123) with
        { ListingLinkMode = mode == "bothCurrent" ? SavedScenarioListingLinkMode.Current : SavedScenarioListingLinkMode.Preserve };
        var updatedListing = mode == "cost" ? null : ListingFactory.ManualOnly("Updated");
        await Drafts(db).SaveAsync(new(Reg(), cost, updatedListing, current.VehicleId, current.Revision), 0);
        var adopted = await Drafts(db).AdoptAsync(1);
        Assert.Equal(3, adopted.Revision);
        Assert.Equal(mode == "listing" ? Write().Input.PriceSek : 123, adopted.Input!.PriceSek);
        Assert.Equal(mode == "cost" ? 1 : 2, adopted.CurrentListingVersion);
        Assert.Equal(mode == "bothCurrent" ? 2 : 1, adopted.SourceListingVersion);
        Assert.Equal(mode is "listing" or "bothPreserve", adopted.NeedsListingReview);
        Assert.Equal(mode == "cost" ? "Volvo V70" : "Updated", (await Listings(db).GetAsync(current.VehicleId))!.ProcessingResult.Listing.VehicleLabel!.Value);
        Assert.Equal(0, (await Transition(db).GetAsync()).Revision);
    }

    [Fact]
    public async Task A_new_vehicle_draft_is_never_rebound_to_a_concurrently_created_vehicle()
    {
        await Fixture.ResetDatabaseAsync();
        await using var first = Fixture.CreateDbContext();
        await using var second = Fixture.CreateDbContext();
        await Drafts(first).SaveAsync(new(Reg(), Write()), 0);
        var car = await Costs(second).CreateAsync(Reg(), Write(5));
        var conflict = await ErrorAsync("registrationNumberConflict", () => Drafts(first).AdoptAsync(1));
        Assert.Equal(car.VehicleId, conflict.VehicleId);
        Assert.Equal(5, (await Costs(second).GetAsync(car.VehicleId))!.Input!.PriceSek);
        Assert.NotNull((await Drafts(first).GetAsync()).Input);
        await ErrorAsync("registrationNumberConflict", () => Drafts(first).SaveAsync(new(Reg(), Write()), 1));
    }

    [Fact]
    public async Task Existing_vehicle_drafts_require_exact_identity_and_fresh_base_revision()
    {
        await Fixture.ResetDatabaseAsync();
        await using var first = Fixture.CreateDbContext();
        await using var second = Fixture.CreateDbContext();
        var car = await Costs(first).CreateAsync(Reg(), Write());
        await ErrorAsync("invalidDraft", () => Drafts(first).SaveAsync(new(Reg(), Write(), BaseVehicleId: car.VehicleId), 0));
        await ErrorAsync("draftIdentityMismatch", () => Drafts(first).SaveAsync(new(Reg("DEF456"), Write(), BaseVehicleId: car.VehicleId, BaseVehicleRevision: 1), 0));
        await Drafts(first).SaveAsync(new(Reg(), Write(), BaseVehicleId: car.VehicleId, BaseVehicleRevision: 1), 0);
        await Costs(second).ReplaceAsync(car.VehicleId, 1, Write(999));
        var conflict = await ErrorAsync("vehicleRevisionConflict", () => Drafts(first).AdoptAsync(1));
        Assert.Equal(2, conflict.ActualRevision);
        Assert.Equal(1, (await Drafts(first).GetAsync()).Revision);
        await ErrorAsync("vehicleRevisionConflict", () => Drafts(first).SaveAsync(new(Reg(), Write(), BaseVehicleId: car.VehicleId, BaseVehicleRevision: 1), 1));
    }

    [Theory]
    [InlineData("current")]
    [InlineData("legacy")]
    [InlineData("legacyConverted")]
    [InlineData("listing")]
    public async Task Every_delete_path_removes_all_vehicle_data_and_matching_draft_without_resetting_slot(string path)
    {
        await Fixture.ResetDatabaseAsync();
        await using var db = Fixture.CreateDbContext();
        var car = await Legacy(db).CreateAsync(Reg(), ScenarioFactory.Complete());
        await Listings(db).ReplaceAsync(car.VehicleId, 1, ListingFactory.Complete());
        if (path != "legacy")
        {
            var pending = await Transition(db).GetAsync();
            await Transition(db).ConfirmAsync(Profile(), 0, pending.Revision, KeepAll(pending));
        }
        var saved = (await Costs(db).GetAsync(car.VehicleId))!;
        var draft = new VehicleDraftInput(Reg(), Write(), BaseVehicleId: car.VehicleId, BaseVehicleRevision: saved.Revision);
        await Drafts(db).SaveAsync(draft, 0);
        if (path == "current") await Costs(db).DeleteAsync(car.VehicleId, saved.Revision);
        else if (path is "legacy" or "legacyConverted") await Legacy(db).DeleteAsync(car.VehicleId, saved.Revision);
        else await Listings(db).DeleteAsync(car.VehicleId, saved.Revision);
        foreach (var table in new[] { "vehicles", "vehicle_cost_inputs", "saved_cost_scenarios", "scenario_energy_sources",
            "scenario_recurring_costs", "scenario_one_time_costs", "vehicle_listings", "listing_sources", "listing_equipment" })
            Assert.Equal(0, await CountAsync(table));
        Assert.Equal(new SavedVehicleDraft(2, null), await Drafts(db).GetAsync());
        await ErrorAsync("draftRevisionConflict", () => Drafts(db).SaveAsync(draft, 1));
        await ErrorAsync("vehicleNotFound", () => Drafts(db).SaveAsync(draft, 2));
        Assert.Equal(path == "legacy" ? 0 : 1, (await new HouseholdProfileStore(db).GetAsync()).Revision);
    }

    [Fact]
    public async Task Failed_adoption_rolls_back_listing_children_even_after_they_were_deleted_inside_transaction()
    {
        await Fixture.ResetDatabaseAsync();
        await using var db = Fixture.CreateDbContext();
        var car = await Listings(db).CreateAsync(Reg(), ListingFactory.Complete());
        await Costs(db).ReplaceAsync(car.VehicleId, 1, Write());
        await Drafts(db).SaveAsync(new(Reg(), Write(50), ListingFactory.ManualOnly(), car.VehicleId, 2), 0);
        // An unreadable current cost format is discovered after the listing child replacement begins.
        await db.Database.ExecuteSqlRawAsync("UPDATE vehicle_cost_inputs SET schema_version = 999");
        await ErrorAsync("unsupportedHouseholdInputVersion", () => Drafts(db).AdoptAsync(1));
        var stillThere = (await Listings(db).GetAsync(car.VehicleId))!;
        Assert.Equal(2, stillThere.Revision);
        Assert.Equal(1, stillThere.ListingVersion);
        Assert.Equal(2, stillThere.ProcessingResult.Listing.Equipment!.Values.Count);
        Assert.Equal(1, await CountAsync("vehicle_cost_inputs"));
        Assert.NotNull((await Drafts(db).GetAsync()).Input);
    }

    [Fact]
    public async Task Database_failure_rolls_back_both_adopted_parts_and_draft_consumption()
    {
        await Fixture.ResetDatabaseAsync();
        await using var db = Fixture.CreateDbContext();
        await Drafts(db).SaveAsync(new(Reg(), Write(), ListingFactory.ManualOnly()), 0);
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE vehicle_draft ADD CONSTRAINT test_reject_consumption CHECK (revision < 2)");
        try
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => Drafts(db).AdoptAsync(1));
            Assert.Equal(0, await CountAsync("vehicles"));
            Assert.Equal(0, await CountAsync("vehicle_cost_inputs"));
            Assert.Equal(0, await CountAsync("vehicle_listings"));
            Assert.Equal(1, (await Drafts(db).GetAsync()).Revision);
            Assert.NotNull((await Drafts(db).GetAsync()).Input);
        }
        finally { await db.Database.ExecuteSqlRawAsync("ALTER TABLE vehicle_draft DROP CONSTRAINT test_reject_consumption"); }
        Assert.NotNull(await Drafts(db).AdoptAsync(1));
    }

    [Fact]
    public async Task Deleting_a_concurrently_created_car_also_clears_a_new_registration_draft()
    {
        await Fixture.ResetDatabaseAsync();
        await using var db = Fixture.CreateDbContext();
        await Drafts(db).SaveAsync(new(Reg(), Write()), 0);
        var car = await Costs(db).CreateAsync(Reg(), Write(1));
        await Costs(db).DeleteAsync(car.VehicleId, 1);
        Assert.Equal(new SavedVehicleDraft(2, null), await Drafts(db).GetAsync());
        await ErrorAsync("draftRevisionConflict", () => Drafts(db).SaveAsync(new(Reg(), Write()), 1));
        Assert.Equal(0, await CountAsync("vehicles"));
    }

    [Fact]
    public async Task Cost_writes_cannot_change_listing_labels_without_reviewed_provenance()
    {
        await Fixture.ResetDatabaseAsync();
        await using var db = Fixture.CreateDbContext();
        var car = await Listings(db).CreateAsync(Reg(), ListingFactory.ManualOnly());
        var withLabel = Write() with { VehicleLabel = "Unreviewed label" };
        await ErrorAsync("listingLabelRequiresReview", () => Costs(db).ReplaceAsync(car.VehicleId, 1, withLabel));
        await ErrorAsync("listingLabelRequiresReview", () => Drafts(db).SaveAsync(new(Reg(), withLabel, BaseVehicleId: car.VehicleId, BaseVehicleRevision: 1), 0));
        Assert.Null((await Listings(db).GetAsync(car.VehicleId))!.ProcessingResult.Listing.VehicleLabel);
        Assert.Equal(0, await CountAsync("vehicle_cost_inputs"));
        Assert.Equal(0, (await Drafts(db).GetAsync()).Revision);
    }
}
