using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Core.Vehicles;
using CarExpenseCalculator.Infrastructure.Persistence;
using CarExpenseCalculator.Infrastructure.Persistence.SavedCostScenarios;
using CarExpenseCalculator.Infrastructure.Persistence.SavedListings;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace CarExpenseCalculator.Infrastructure.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class SavedListingStoreTests(PostgreSqlFixture fixture)
{
    private static readonly DateTimeOffset InitialTime =
        new(2026, 9, 4, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Create_and_read_round_trip_every_listing_storage_shape()
    {
        await fixture.ResetDatabaseAsync();
        await using var dbContext = fixture.CreateDbContext();
        var store = CreateListingStore(dbContext);

        var created = await store.CreateAsync(
            RegistrationNumber.Parse(" abc-123 "),
            ListingFactory.Complete());
        var byId = await store.GetAsync(created.VehicleId);
        var byRegistration = await store.GetByRegistrationNumberAsync(
            RegistrationNumber.Parse("ABC123"));

        Assert.Equal(7, created.VehicleId.Version);
        Assert.Equal("ABC123", created.RegistrationNumber.Value);
        Assert.Equal(1, created.Revision);
        Assert.Equal(1, created.ListingVersion);
        Assert.Equal(1, created.ListingSchemaVersion);
        Assert.Equal("https://EXAMPLE.com/listings/abc123?campaign=Autumn#details", created.SubmittedUrl);
        Assert.Equal("https://example.com/listings/abc123?campaign=Autumn", created.NormalizedUrl.Value);
        Assert.Equal("gpt-5.6-luna", created.RequestedModel);
        Assert.Equal(2, created.PromptVersion);
        Assert.Equal(2, created.ExtractionSchemaVersion);
        Assert.False(created.HasSavedCostScenario);
        AssertSavedListing(created, byId);
        AssertSavedListing(created, byRegistration);

        var listing = created.ProcessingResult.Listing;
        Assert.Equal(ListingAnalysisStatus.Complete, created.ProcessingResult.Status);
        Assert.Empty(created.ProcessingResult.MissingFields);
        Assert.Equal("Volvo V70", listing.VehicleLabel!.Value);
        Assert.Equal("Volvo", listing.Make!.Value);
        Assert.Equal("YV1BW1234G1234567", listing.Vin!.Value);
        Assert.Equal("Jönköpings län", listing.County!.Value);
        Assert.Equal("Blå", listing.Colour!.Value);
        Assert.Equal(123_456.123456789012345m, listing.PriceSek!.Value);
        Assert.Equal(1969.123456789012345m, listing.EngineDisplacementCubicCentimetres!.Value);
        Assert.Equal([FuelType.Diesel, FuelType.Electricity], listing.FuelTypes!.Values);
        Assert.Equal(["Diesel", "El"], listing.EnergyConsumptions!.Values.Select(value => value.Label));
        Assert.Equal(["Dragkrok", "Värmare"], listing.Equipment!.Values);
        Assert.Equal(["Full servicehistorik", "Inga kända skulder"], listing.SellerClaims!.Values);
        Assert.Equal(["Mindre bruksspår"], listing.ConditionNotes!.Values);
        Assert.False(listing.TowBar!.Value);
        Assert.Equal(FieldOrigin.Listing, listing.County.Provenance.Origin);
        Assert.Equal(FieldOrigin.User, listing.VehicleLabel.Provenance.Origin);
        Assert.All(
            EnumerateProvenance(listing),
            provenance => Assert.Equal(created.NormalizedUrl, provenance.SourceUrl));
        Assert.Equal(
            [
                "https://example.com/listings/abc123?source=search",
                "https://example.com/dealer",
            ],
            created.ProcessingResult.Sources.Select(source => source.Url.Value));
        Assert.Equal([true, false], created.ProcessingResult.Sources.Select(source => source.MatchesSubmittedUrl));

        Assert.Equal(
            "array",
            await ScalarAsync<string>("SELECT jsonb_typeof(energy_consumptions) FROM vehicle_listings"));
        Assert.Equal(
            "array",
            await ScalarAsync<string>("SELECT jsonb_typeof(seller_claims) FROM vehicle_listings"));
        Assert.Equal(
            "object",
            await ScalarAsync<string>("SELECT jsonb_typeof(field_provenance) FROM vehicle_listings"));
    }

    [Fact]
    public async Task Manual_only_save_injects_registration_and_preserves_null_zero_false_and_known_empty()
    {
        await fixture.ResetDatabaseAsync();
        await using var dbContext = fixture.CreateDbContext();
        var store = CreateListingStore(dbContext);

        var saved = await store.CreateAsync(
            RegistrationNumber.Parse("ABC12D"),
            ListingFactory.ManualOnly());

        var listing = saved.ProcessingResult.Listing;
        Assert.Equal("ABC12D", listing.RegistrationNumber!.Value.Value);
        Assert.Equal(FieldOrigin.User, listing.RegistrationNumber.Provenance.Origin);
        Assert.Null(listing.Locality);
        Assert.Null(listing.County);
        Assert.Equal(0m, listing.PriceSek!.Value);
        Assert.Equal(0m, listing.OdometerKilometres!.Value);
        Assert.Equal(0, listing.ImageCount!.Value);
        Assert.Equal(0m, listing.AnnualVehicleTaxSek!.Value);
        Assert.Equal(0, listing.OwnerCount!.Value);
        Assert.False(listing.TowBar!.Value);
        Assert.Empty(listing.FuelTypes!.Values);
        Assert.Empty(listing.EnergyConsumptions!.Values);
        Assert.Empty(listing.Equipment!.Values);
        Assert.Empty(listing.SellerClaims!.Values);
        Assert.Empty(listing.ConditionNotes!.Values);
        Assert.DoesNotContain(ListingFieldCode.RegistrationNumber, saved.ProcessingResult.MissingFields);
        Assert.DoesNotContain(ListingFieldCode.FuelTypes, saved.ProcessingResult.MissingFields);
        Assert.Contains(ListingFieldCode.Locality, saved.ProcessingResult.MissingFields);
        Assert.Contains(ListingFieldCode.County, saved.ProcessingResult.MissingFields);
        Assert.Equal(ListingAnalysisStatus.Unavailable, saved.ProcessingResult.Status);
        Assert.Null(saved.RequestedModel);
        Assert.Null(saved.PromptVersion);
        Assert.Null(saved.ExtractionSchemaVersion);

        var unknownInput = ListingFactory.ManualOnly(knownEmptyCollections: false);
        var unknownUrl = ListingUrl.Parse(unknownInput.SubmittedUrl);
        unknownInput = new SavedListingInput(
            unknownInput.SubmittedUrl,
            unknownInput.AnalyzedAtUtc,
            null,
            null,
            null,
            unknownInput.Sources,
            unknownInput.Listing with
            {
                Locality = new SourcedValue<string>("  Visby  ", ListingFactory.Manual(unknownUrl)),
                County = null,
            });
        var unknown = await store.CreateAsync(
            RegistrationNumber.Parse("DEF456"),
            unknownInput);
        Assert.Equal("Visby", unknown.ProcessingResult.Listing.Locality!.Value);
        Assert.Null(unknown.ProcessingResult.Listing.County);
        Assert.Null(unknown.ProcessingResult.Listing.FuelTypes);
        Assert.Null(unknown.ProcessingResult.Listing.EnergyConsumptions);
        Assert.Null(unknown.ProcessingResult.Listing.Equipment);
        Assert.Null(unknown.ProcessingResult.Listing.SellerClaims);
        Assert.Null(unknown.ProcessingResult.Listing.ConditionNotes);
    }

    [Fact]
    public async Task Create_reports_existing_vehicle_identity_and_revision_for_any_duplicate_registration()
    {
        await fixture.ResetDatabaseAsync();
        SavedCostScenario scenario;
        await using (var context = fixture.CreateDbContext())
        {
            scenario = await CreateScenarioStore(context).CreateAsync(
                RegistrationNumber.Parse("ABC123"),
                ScenarioFactory.Complete());
        }

        await using var dbContext = fixture.CreateDbContext();
        var store = CreateListingStore(dbContext);
        var exception = await Assert.ThrowsAsync<SavedListingRegistrationConflictException>(
            () => store.CreateAsync(
                RegistrationNumber.Parse("abc-123"),
                ListingFactory.Complete()));

        Assert.Equal(scenario.VehicleId, exception.ExistingVehicleId);
        Assert.Equal(scenario.Revision, exception.ActualRevision);
        Assert.Equal("ABC123", exception.RegistrationNumber.Value);
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM vehicles"));
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM vehicle_listings"));
    }

    [Fact]
    public async Task Create_rejects_registration_mismatch_and_ai_without_extraction_metadata()
    {
        await fixture.ResetDatabaseAsync();
        await using var dbContext = fixture.CreateDbContext();
        var store = CreateListingStore(dbContext);

        var mismatch = await Assert.ThrowsAsync<ListingValidationException>(
            () => store.CreateAsync(
                RegistrationNumber.Parse("ABC123"),
                ListingFactory.Complete("DEF456")));
        Assert.Contains(
            mismatch.Errors,
            error => error.Path == "registrationNumber.value");

        var extracted = ListingFactory.Complete();
        var missingMetadata = new SavedListingInput(
            extracted.SubmittedUrl,
            extracted.AnalyzedAtUtc,
            null,
            null,
            null,
            extracted.Sources,
            extracted.Listing);
        var metadataError = await Assert.ThrowsAsync<ListingValidationException>(
            () => store.CreateAsync(
                RegistrationNumber.Parse("ABC123"),
                missingMetadata));
        Assert.Contains(metadataError.Errors, error => error.Path == "requestedModel");
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM vehicles"));
    }

    [Fact]
    public async Task Stores_filter_resources_and_can_attach_both_sides_of_an_aggregate()
    {
        await fixture.ResetDatabaseAsync();
        SavedCostScenario scenarioOnly;
        SavedListing listingOnly;
        await using (var context = fixture.CreateDbContext())
        {
            scenarioOnly = await CreateScenarioStore(context).CreateAsync(
                RegistrationNumber.Parse("ABC123"),
                ScenarioFactory.Complete("Scenario first"));
        }

        await using (var context = fixture.CreateDbContext())
        {
            listingOnly = await CreateListingStore(context).CreateAsync(
                RegistrationNumber.Parse("DEF456"),
                ListingFactory.ManualOnly("Listing first"));
        }

        await using (var context = fixture.CreateDbContext())
        {
            Assert.Single(await CreateScenarioStore(context).ListAsync());
            Assert.Single(await CreateListingStore(context).ListAsync());
            Assert.Null(await CreateListingStore(context).GetAsync(scenarioOnly.VehicleId));
            Assert.Null(await CreateScenarioStore(context).GetAsync(listingOnly.VehicleId));
        }

        SavedListing combinedFromScenario;
        await using (var context = fixture.CreateDbContext())
        {
            combinedFromScenario = await CreateListingStore(context).ReplaceAsync(
                scenarioOnly.VehicleId,
                scenarioOnly.Revision,
                ListingFactory.Complete());
        }

        Assert.Equal(2, combinedFromScenario.Revision);
        Assert.Equal(1, combinedFromScenario.ListingVersion);
        Assert.True(combinedFromScenario.HasSavedCostScenario);

        SavedCostScenario combinedFromListing;
        await using (var context = fixture.CreateDbContext())
        {
            combinedFromListing = await CreateScenarioStore(context).ReplaceAsync(
                listingOnly.VehicleId,
                listingOnly.Revision,
                ScenarioFactory.Complete("Calculation attached"),
                SavedScenarioListingLinkMode.Preserve);
        }

        Assert.Equal(2, combinedFromListing.Revision);
        Assert.Null(combinedFromListing.SourceListingVersion);
        Assert.Equal(1, combinedFromListing.CurrentListingVersion);
        Assert.True(combinedFromListing.HasSavedListing);
        await using (var context = fixture.CreateDbContext())
        {
            var preservedListing = await CreateListingStore(context).GetAsync(listingOnly.VehicleId);
            Assert.NotNull(preservedListing);
            Assert.True(preservedListing.HasSavedCostScenario);
            Assert.Equal(1, preservedListing.ListingVersion);
            Assert.Equal("Calculation attached", preservedListing.ProcessingResult.Listing.VehicleLabel!.Value);
        }
    }

    [Fact]
    public async Task Listing_and_scenario_replacements_increment_only_their_owned_versions()
    {
        await fixture.ResetDatabaseAsync();
        var time = new MutableTimeProvider(InitialTime);
        SavedListing created;
        await using (var context = fixture.CreateDbContext())
        {
            created = await CreateListingStore(context, time).CreateAsync(
                RegistrationNumber.Parse("ABC123"),
                ListingFactory.Complete());
        }

        SavedCostScenario attachedScenario;
        time.Advance(TimeSpan.FromMinutes(1));
        await using (var context = fixture.CreateDbContext())
        {
            attachedScenario = await CreateScenarioStore(context, time).ReplaceAsync(
                created.VehicleId,
                created.Revision,
                ScenarioFactory.Complete("Scenario label"),
                SavedScenarioListingLinkMode.Preserve);
        }

        time.Advance(TimeSpan.FromMinutes(1));
        await using (var context = fixture.CreateDbContext())
        {
            var afterScenario = await CreateListingStore(context, time).GetAsync(created.VehicleId);
            Assert.NotNull(afterScenario);
            Assert.Equal(2, afterScenario.Revision);
            Assert.Equal(1, afterScenario.ListingVersion);
            Assert.Equal("Scenario label", afterScenario.ProcessingResult.Listing.VehicleLabel!.Value);
        }

        var oldChildIds = await ListingChildIdsAsync();
        SavedListing replaced;
        await using (var context = fixture.CreateDbContext())
        {
            replaced = await CreateListingStore(context, time).ReplaceAsync(
                created.VehicleId,
                attachedScenario.Revision,
                ListingFactory.ManualOnly(vehicleLabel: null));
        }

        Assert.Equal(3, replaced.Revision);
        Assert.Equal(2, replaced.ListingVersion);
        Assert.True(replaced.HasSavedCostScenario);
        Assert.Null(replaced.ProcessingResult.Listing.VehicleLabel);
        Assert.Empty(oldChildIds.Intersect(await ListingChildIdsAsync()));
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM listing_sources"));
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM listing_equipment"));
        Assert.Equal("Saab", replaced.ProcessingResult.Listing.Make!.Value);
        Assert.Null(replaced.ProcessingResult.Listing.Model);
        Assert.Equal(
            "[]",
            await ScalarAsync<string>("SELECT energy_consumptions::text FROM vehicle_listings"));
        Assert.DoesNotContain(
            "county",
            await ScalarAsync<string>("SELECT field_provenance::text FROM vehicle_listings"),
            StringComparison.Ordinal);

        await using var verificationContext = fixture.CreateDbContext();
        var preservedScenario = await CreateScenarioStore(verificationContext).GetAsync(created.VehicleId);
        Assert.NotNull(preservedScenario);
        Assert.Equal(3, preservedScenario.Revision);
        Assert.Null(preservedScenario.Scenario.VehicleLabel);
        Assert.Equal(123_456.123456789012345m, preservedScenario.Scenario.PurchasePriceSek);
    }

    [Fact]
    public async Task Listing_link_modes_preserve_assumptions_and_report_current_or_outdated_versions()
    {
        await fixture.ResetDatabaseAsync();
        SavedListing listing;
        await using (var context = fixture.CreateDbContext())
        {
            listing = await CreateListingStore(context).CreateAsync(
                RegistrationNumber.Parse("ABC123"),
                ListingFactory.Complete());
        }

        SavedCostScenario linked;
        await using (var context = fixture.CreateDbContext())
        {
            linked = await CreateScenarioStore(context).ReplaceAsync(
                listing.VehicleId,
                listing.Revision,
                ScenarioFactory.Complete("Linked"),
                SavedScenarioListingLinkMode.Current);
        }

        Assert.Equal(1, linked.SourceListingVersion);
        Assert.Equal(1, linked.CurrentListingVersion);
        Assert.True(linked.HasSavedListing);
        await using (var context = fixture.CreateDbContext())
        {
            var current = await CreateListingStore(context).GetAsync(listing.VehicleId);
            Assert.NotNull(current);
            Assert.Equal(1, current.SavedCostScenarioSourceListingVersion);
            Assert.False(current.SavedCostScenarioOutdated);
        }

        SavedListing refreshed;
        await using (var context = fixture.CreateDbContext())
        {
            refreshed = await CreateListingStore(context).ReplaceAsync(
                listing.VehicleId,
                linked.Revision,
                ListingFactory.ManualOnly("Refreshed"));
        }

        Assert.Equal(2, refreshed.ListingVersion);
        Assert.Equal(1, refreshed.SavedCostScenarioSourceListingVersion);
        Assert.True(refreshed.SavedCostScenarioOutdated);

        SavedCostScenario preserved;
        await using (var context = fixture.CreateDbContext())
        {
            preserved = await CreateScenarioStore(context).ReplaceAsync(
                listing.VehicleId,
                refreshed.Revision,
                ScenarioFactory.Replacement(),
                SavedScenarioListingLinkMode.Preserve);
        }

        Assert.Equal(1, preserved.SourceListingVersion);
        Assert.Equal(2, preserved.CurrentListingVersion);

        SavedCostScenario reviewed;
        await using (var context = fixture.CreateDbContext())
        {
            reviewed = await CreateScenarioStore(context).ReplaceAsync(
                listing.VehicleId,
                preserved.Revision,
                ScenarioFactory.Replacement(),
                SavedScenarioListingLinkMode.Current);
        }

        Assert.Equal(2, reviewed.SourceListingVersion);
        Assert.Equal(2, reviewed.CurrentListingVersion);
        await using (var context = fixture.CreateDbContext())
        {
            var current = await CreateListingStore(context).GetAsync(listing.VehicleId);
            Assert.NotNull(current);
            Assert.False(current.SavedCostScenarioOutdated);
        }
    }

    [Fact]
    public async Task Current_link_mode_requires_a_listing_and_changes_nothing_when_unavailable()
    {
        await fixture.ResetDatabaseAsync();
        SavedCostScenario scenario;
        await using (var context = fixture.CreateDbContext())
        {
            scenario = await CreateScenarioStore(context).CreateAsync(
                RegistrationNumber.Parse("ABC123"),
                ScenarioFactory.Complete());
        }

        await using (var context = fixture.CreateDbContext())
        {
            await Assert.ThrowsAsync<SavedScenarioListingLinkUnavailableException>(() =>
                CreateScenarioStore(context).ReplaceAsync(
                    scenario.VehicleId,
                    scenario.Revision,
                    ScenarioFactory.Replacement(),
                    SavedScenarioListingLinkMode.Current));
        }

        await using (var context = fixture.CreateDbContext())
        {
            var unchanged = await CreateScenarioStore(context).GetAsync(scenario.VehicleId);
            Assert.NotNull(unchanged);
            Assert.Equal(1, unchanged.Revision);
            Assert.Null(unchanged.SourceListingVersion);
            Assert.Null(unchanged.CurrentListingVersion);
            Assert.False(unchanged.HasSavedListing);
            Assert.Equal(scenario.Scenario.VehicleLabel, unchanged.Scenario.VehicleLabel);
        }
    }

    [Fact]
    public async Task Stale_replace_and_delete_leave_the_current_aggregate_unchanged()
    {
        await fixture.ResetDatabaseAsync();
        SavedListing created;
        await using (var context = fixture.CreateDbContext())
        {
            created = await CreateListingStore(context).CreateAsync(
                RegistrationNumber.Parse("ABC123"),
                ListingFactory.Complete());
        }

        SavedListing current;
        await using (var context = fixture.CreateDbContext())
        {
            current = await CreateListingStore(context).ReplaceAsync(
                created.VehicleId,
                created.Revision,
                ListingFactory.ManualOnly("Current"));
        }

        await using (var context = fixture.CreateDbContext())
        {
            var exception = await Assert.ThrowsAsync<SavedListingConcurrencyException>(
                () => CreateListingStore(context).ReplaceAsync(
                    created.VehicleId,
                    created.Revision,
                    ListingFactory.ManualOnly("Stale")));
            Assert.Equal(current.Revision, exception.ActualRevision);
        }

        await using (var context = fixture.CreateDbContext())
        {
            var exception = await Assert.ThrowsAsync<SavedListingConcurrencyException>(
                () => CreateListingStore(context).DeleteAsync(
                    created.VehicleId,
                    created.Revision));
            Assert.Equal(current.Revision, exception.ActualRevision);
        }

        await using var verificationContext = fixture.CreateDbContext();
        var unchanged = await CreateListingStore(verificationContext).GetAsync(created.VehicleId);
        Assert.NotNull(unchanged);
        Assert.Equal("Current", unchanged.ProcessingResult.Listing.VehicleLabel!.Value);
        Assert.Equal(2, unchanged.Revision);
        Assert.Equal(2, unchanged.ListingVersion);
    }

    [Fact]
    public async Task Missing_listing_resources_produce_typed_outcomes()
    {
        await fixture.ResetDatabaseAsync();
        var missingId = Guid.CreateVersion7();
        await using var dbContext = fixture.CreateDbContext();
        var store = CreateListingStore(dbContext);

        Assert.Null(await store.GetAsync(missingId));
        await Assert.ThrowsAsync<SavedListingNotFoundException>(
            () => store.ReplaceAsync(missingId, 1, ListingFactory.ManualOnly()));
        await Assert.ThrowsAsync<SavedListingNotFoundException>(
            () => store.DeleteAsync(missingId, 1));
    }

    [Fact]
    public async Task Delete_removes_the_complete_combined_aggregate_and_all_children()
    {
        await fixture.ResetDatabaseAsync();
        SavedCostScenario scenario;
        await using (var context = fixture.CreateDbContext())
        {
            scenario = await CreateScenarioStore(context).CreateAsync(
                RegistrationNumber.Parse("ABC123"),
                ScenarioFactory.Complete());
        }

        SavedListing listing;
        await using (var context = fixture.CreateDbContext())
        {
            listing = await CreateListingStore(context).ReplaceAsync(
                scenario.VehicleId,
                scenario.Revision,
                ListingFactory.Complete());
        }

        await using (var context = fixture.CreateDbContext())
        {
            await CreateListingStore(context).DeleteAsync(listing.VehicleId, listing.Revision);
        }

        foreach (var table in new[]
                 {
                     "vehicles",
                     "vehicle_listings",
                     "listing_sources",
                     "listing_equipment",
                     "saved_cost_scenarios",
                     "scenario_energy_sources",
                     "scenario_recurring_costs",
                     "scenario_one_time_costs",
                 })
        {
            Assert.Equal(0L, await ScalarAsync<long>($"SELECT count(*) FROM {table}"));
        }
    }

    [Fact]
    public async Task List_orders_by_aggregate_update_time_then_uuid()
    {
        await fixture.ResetDatabaseAsync();
        var time = new MutableTimeProvider(InitialTime);
        SavedListing first;
        SavedListing second;
        await using (var context = fixture.CreateDbContext())
        {
            first = await CreateListingStore(context, time).CreateAsync(
                RegistrationNumber.Parse("ABC123"),
                ListingFactory.ManualOnly("First"));
        }

        time.Advance(TimeSpan.FromMinutes(1));
        await using (var context = fixture.CreateDbContext())
        {
            second = await CreateListingStore(context, time).CreateAsync(
                RegistrationNumber.Parse("DEF456"),
                ListingFactory.ManualOnly("Second"));
        }

        await using var dbContext = fixture.CreateDbContext();
        var saved = await CreateListingStore(dbContext, time).ListAsync();
        Assert.Equal([second.VehicleId, first.VehicleId], saved.Select(item => item.VehicleId));
    }

    [Theory]
    [InlineData(
        "ALTER TABLE vehicle_listings DROP CONSTRAINT ck_vehicle_listings_versions; UPDATE vehicle_listings SET listing_schema_version = 99",
        99,
        2,
        2)]
    [InlineData(
        "ALTER TABLE vehicle_listings DROP CONSTRAINT ck_vehicle_listings_extraction_metadata; UPDATE vehicle_listings SET prompt_version = 99",
        1,
        99,
        2)]
    [InlineData(
        "ALTER TABLE vehicle_listings DROP CONSTRAINT ck_vehicle_listings_extraction_metadata; UPDATE vehicle_listings SET extraction_schema_version = 99",
        1,
        2,
        99)]
    public async Task Stored_unsupported_versions_are_rejected(
        string corruptionSql,
        int expectedListingSchemaVersion,
        int expectedPromptVersion,
        int expectedExtractionSchemaVersion)
    {
        await fixture.ResetDatabaseAsync();
        SavedListing created;
        await using (var context = fixture.CreateDbContext())
        {
            created = await CreateListingStore(context).CreateAsync(
                RegistrationNumber.Parse("ABC123"),
                ListingFactory.Complete());
        }

        await ExecuteNonQueryAsync(corruptionSql);

        await using var dbContext = fixture.CreateDbContext();
        var exception = await Assert.ThrowsAsync<UnsupportedSavedListingVersionException>(
            () => CreateListingStore(dbContext).GetAsync(created.VehicleId));
        Assert.Equal(expectedListingSchemaVersion, exception.ListingSchemaVersion);
        Assert.Equal(expectedPromptVersion, exception.PromptVersion);
        Assert.Equal(expectedExtractionSchemaVersion, exception.ExtractionSchemaVersion);
    }

    [Theory]
    [InlineData("UPDATE vehicle_listings SET price_sek = -1")]
    [InlineData("UPDATE vehicle_listings SET locality = repeat('x', 101)")]
    [InlineData("UPDATE vehicle_listings SET fuel_types = ARRAY['Steam']::text[]")]
    [InlineData("UPDATE vehicle_listings SET energy_consumptions = '{}'::jsonb")]
    [InlineData("UPDATE vehicle_listings SET energy_consumptions = '[{}, {}, {}]'::jsonb")]
    [InlineData("UPDATE vehicle_listings SET seller_claims = '[0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20]'::jsonb")]
    [InlineData("UPDATE vehicle_listings SET field_provenance = '[]'::jsonb")]
    [InlineData("UPDATE vehicle_listings SET prompt_version = NULL")]
    [InlineData("UPDATE listing_sources SET position = -1")]
    [InlineData("UPDATE listing_sources SET position = 0")]
    [InlineData("UPDATE listing_sources SET url = ' '")]
    [InlineData("UPDATE listing_equipment SET position = 100")]
    [InlineData("UPDATE listing_equipment SET position = 0")]
    [InlineData("UPDATE listing_equipment SET value = ' '")]
    public async Task Database_constraints_reject_invalid_listing_storage(string sql)
    {
        await fixture.ResetDatabaseAsync();
        await using (var context = fixture.CreateDbContext())
        {
            await CreateListingStore(context).CreateAsync(
                RegistrationNumber.Parse("ABC123"),
                ListingFactory.Complete());
        }

        await Assert.ThrowsAsync<PostgresException>(() => ExecuteNonQueryAsync(sql));
    }

    private static SavedListingStore CreateListingStore(
        CarExpenseDbContext dbContext,
        TimeProvider? timeProvider = null) =>
        new(
            dbContext,
            new ListingDraftProcessor(),
            timeProvider ?? new MutableTimeProvider(InitialTime));

    private static SavedCostScenarioStore CreateScenarioStore(
        CarExpenseDbContext dbContext,
        TimeProvider? timeProvider = null) =>
        new(
            dbContext,
            new CostScenarioCalculator(),
            timeProvider ?? new MutableTimeProvider(InitialTime));

    private static void AssertSavedListing(SavedListing expected, SavedListing? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.VehicleId, actual.VehicleId);
        Assert.Equal(expected.RegistrationNumber, actual.RegistrationNumber);
        Assert.Equal(expected.Revision, actual.Revision);
        Assert.Equal(expected.ListingVersion, actual.ListingVersion);
        Assert.Equal(expected.ListingSchemaVersion, actual.ListingSchemaVersion);
        Assert.Equal(expected.NormalizedUrl, actual.NormalizedUrl);
        Assert.Equal(expected.RequestedModel, actual.RequestedModel);
        Assert.Equal(expected.CreatedAtUtc, actual.CreatedAtUtc);
        Assert.Equal(expected.UpdatedAtUtc, actual.UpdatedAtUtc);
        Assert.Equivalent(expected.ProcessingResult.Listing, actual.ProcessingResult.Listing, strict: true);
    }

    private static IEnumerable<FieldProvenance> EnumerateProvenance(ListingDraft listing)
    {
        var properties = typeof(ListingDraft).GetProperties();
        foreach (var property in properties)
        {
            var value = property.GetValue(listing);
            var provenance = value?.GetType().GetProperty("Provenance")?.GetValue(value);
            if (provenance is FieldProvenance typed)
            {
                yield return typed;
            }
        }
    }

    private async Task<IReadOnlyList<Guid>> ListingChildIdsAsync()
    {
        var ids = new List<Guid>();
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM listing_sources UNION ALL SELECT id FROM listing_equipment";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids;
    }

    private async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)(await command.ExecuteScalarAsync())!;
    }

    private async Task ExecuteNonQueryAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
