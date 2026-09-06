using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Core.Households;
using CarExpenseCalculator.Infrastructure.Persistence.Households;
using CarExpenseCalculator.Infrastructure.Persistence.SavedCostScenarios;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CarExpenseCalculator.Infrastructure.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class HouseholdTransitionStoreTests(PostgreSqlFixture fixture) : HouseholdTestSupport(fixture)
{
    [Fact]
    public async Task Confirmation_preserves_identity_residual_listing_and_review_facts_without_old_household_assumptions()
    {
        await Fixture.ResetDatabaseAsync();
        await using var writer = Fixture.CreateDbContext();
        var first = await Legacy(writer).CreateAsync(Reg(), ScenarioFactory.Complete());
        await Listings(writer).ReplaceAsync(first.VehicleId, 1, ListingFactory.Complete());
        await Legacy(writer).ReplaceAsync(first.VehicleId, 2, first.Scenario, SavedScenarioListingLinkMode.Current);
        await Legacy(writer).CreateAsync(Reg("DEF456"), ScenarioFactory.Replacement());
        await using var reader = Fixture.CreateDbContext();
        var snapshot = await Transition(reader).GetAsync();
        Assert.Null(snapshot.Profile.Input);
        Assert.Equal(4, snapshot.Revision);
        Assert.Equal(2, snapshot.Vehicles.Count);
        var firstReview = snapshot.Vehicles.Single(x => x.VehicleId == first.VehicleId);
        Assert.Equal(1, firstReview.SourceListingVersion);
        Assert.Equal(first.Scenario.ExpectedResidualValueSek, firstReview.Legacy!.SuggestedInput.Residual!.Value!.Single);
        Assert.Equal(24, firstReview.Legacy.SuggestedInput.Residual.PeriodMonths);
        var energy = firstReview.Legacy.Items.First(x => x.Kind == LegacyItemKind.Energy);
        Assert.Null(energy.AmountSek);
        Assert.Contains("energy", energy.AffectedSections);
        Assert.Equal("energyIdentityAndConsumptionBasisRequireReview", energy.Reason);
        var result = await Transition(writer).ConfirmAsync(Profile(), 0, snapshot.Revision, KeepAll(snapshot));
        Assert.Equal(1, result.Profile.Revision);
        Assert.Empty(result.Vehicles);
        Assert.Equal(5, result.Revision);
        var saved = (await Costs(reader).GetAsync(first.VehicleId))!;
        Assert.Equal(4, saved.Revision);
        Assert.Equal(firstReview.CreatedAtUtc, saved.CreatedAtUtc);
        Assert.Equal(1, saved.SourceListingVersion);
        Assert.False(saved.NeedsListingReview);
        Assert.Equal(firstReview.Legacy.Items, saved.UnresolvedLegacyItems);
        Assert.Null(saved.Legacy);
        Assert.Equal(first.Scenario.PurchasePriceSek, saved.Input!.PriceSek);
        Assert.Equal(0, await CountAsync("saved_cost_scenarios"));
        Assert.Equal(0, await CountAsync("scenario_energy_sources"));
        Assert.Equal(0, await CountAsync("scenario_recurring_costs"));
        Assert.Equal(0, await CountAsync("scenario_one_time_costs"));
        var json = await ScalarAsync<string>("SELECT input::text FROM vehicle_cost_inputs LIMIT 1");
        Assert.DoesNotContain("pricePerUnitSek", json);
        Assert.DoesNotContain("distanceSharePercent", json);
        Assert.DoesNotContain("financing", json);
        await ErrorAsync("householdTransitionRequired", () => Legacy(writer).ReplaceAsync(first.VehicleId, 4, first.Scenario, SavedScenarioListingLinkMode.Preserve));
    }

    [Fact]
    public async Task Legacy_maximum_collections_are_preserved_until_explicit_mapping_or_discard()
    {
        await Fixture.ResetDatabaseAsync();
        await using var db = Fixture.CreateDbContext();
        var old = new CostScenario("Maximum", 12, 100000, 80000, 0, null, [], null, null,
            new(12000, RecurringCostCadence.Annual),
            Enumerable.Range(0, 50).Select(x => new NamedRecurringCost($"Recurring {x}", x + 1, RecurringCostCadence.Monthly)),
            Enumerable.Range(0, 50).Select(x => new OneTimeCost($"Once {x}", x + 51)));
        await Legacy(db).CreateAsync(Reg(), old);
        var review = await Transition(db).GetAsync();
        var car = Assert.Single(review.Vehicles);
        Assert.Equal(101, car.Legacy!.Items.Count);
        Assert.Equal(50, car.Legacy.SuggestedInput.CustomCosts!.Items.Count);
        var decisions = car.Legacy.Items.Select(x => new LegacyItemDecision(x.Key,
            x.Kind == LegacyItemKind.Recurring ? LegacyItemDisposition.Map : LegacyItemDisposition.KeepForReview,
            x.Kind == LegacyItemKind.Recurring ? x.Key : null)).ToArray();
        await Transition(db).ConfirmAsync(new(), 0, review.Revision,
            [new(car.VehicleId, car.Revision, new(car.Legacy.SuggestedInput, "Maximum", LegacyDecisions: decisions))]);
        var saved = (await Costs(db).GetAsync(car.VehicleId))!;
        Assert.Equal(51, saved.UnresolvedLegacyItems.Count);
        Assert.Equal(Enumerable.Range(51, 50).Select(x => (decimal?)x), saved.UnresolvedLegacyItems.Where(x => x.Kind == LegacyItemKind.OneTime).Select(x => x.AmountSek));
        Assert.Equal(50, saved.Input!.CustomCosts!.Items.Count);
        var retained = await Costs(db).ReplaceAsync(car.VehicleId, saved.Revision, new(saved.Input));
        Assert.Equal(saved.UnresolvedLegacyItems, retained.UnresolvedLegacyItems);
        var explicitDiscard = retained.UnresolvedLegacyItems.Select(x => new LegacyItemDecision(x.Key, LegacyItemDisposition.Discard)).ToArray();
        var cleared = await Costs(db).ReplaceAsync(car.VehicleId, retained.Revision, new(saved.Input, LegacyDecisions: explicitDiscard));
        Assert.Empty(cleared.UnresolvedLegacyItems);
        Assert.Equal(1, await CountAsync("vehicle_cost_inputs"));
        Assert.DoesNotContain("Once 49", await ScalarAsync<string>("SELECT input::text FROM vehicle_cost_inputs"));
    }

    [Theory]
    [InlineData("profile")]
    [InlineData("cost")]
    [InlineData("draft")]
    public async Task Direct_saves_cannot_bypass_confirmation(string operation)
    {
        await Fixture.ResetDatabaseAsync();
        await using var db = Fixture.CreateDbContext();
        var legacy = await Legacy(db).CreateAsync(Reg(), ScenarioFactory.Complete());
        if (operation == "profile")
            await ErrorAsync("householdTransitionRequired", () => new HouseholdProfileStore(db).SaveAsync(new(), 0));
        else if (operation == "cost")
            await ErrorAsync("householdTransitionRequired", () => Costs(db).ReplaceAsync(legacy.VehicleId, 1, Write()));
        else
        {
            await Drafts(db).SaveAsync(new(Reg(), Write(), BaseVehicleId: legacy.VehicleId, BaseVehicleRevision: 1), 0);
            await ErrorAsync("householdTransitionRequired", () => Drafts(db).AdoptAsync(1));
            Assert.NotNull((await Drafts(db).GetAsync()).Input);
        }
        Assert.Equal(1, await CountAsync("saved_cost_scenarios"));
        Assert.Equal(0, await CountAsync("vehicle_cost_inputs"));
        Assert.Equal(0, (await new HouseholdProfileStore(db).GetAsync()).Revision);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("unknown")]
    [InlineData("missingTarget")]
    [InlineData("keptAndIncluded")]
    [InlineData("discardedAndIncluded")]
    [InlineData("mappedAndDuplicated")]
    public async Task Invalid_item_accounting_rolls_back_the_entire_transition(string failure)
    {
        await Fixture.ResetDatabaseAsync();
        await using var db = Fixture.CreateDbContext();
        await Legacy(db).CreateAsync(Reg(), ScenarioFactory.Complete());
        await Legacy(db).CreateAsync(Reg("DEF456"), ScenarioFactory.Complete());
        var review = await Transition(db).GetAsync();
        var writes = KeepAll(review);
        var second = review.Vehicles[1];
        var decisions = writes[1].Cost.LegacyDecisions!.ToArray();
        VehicleCostWrite invalid = writes[1].Cost;
        if (failure == "missing") invalid = invalid with { LegacyDecisions = null };
        if (failure == "duplicate") { decisions[1] = decisions[0]; invalid = invalid with { LegacyDecisions = decisions }; }
        if (failure == "unknown") { decisions[0] = decisions[0] with { Key = "invented" }; invalid = invalid with { LegacyDecisions = decisions }; }
        if (failure == "missingTarget") { decisions[0] = decisions[0] with { Disposition = LegacyItemDisposition.Map, TargetKey = "absent" }; invalid = invalid with { LegacyDecisions = decisions }; }
        if (failure is "keptAndIncluded" or "discardedAndIncluded")
        {
            invalid = invalid with { Input = second.Legacy!.SuggestedInput };
            if (failure == "discardedAndIncluded") invalid = invalid with
            { LegacyDecisions = decisions.Select(x => x with { Disposition = LegacyItemDisposition.Discard }).ToArray() };
        }
        if (failure == "mappedAndDuplicated")
        {
            var source = second.Legacy!.Items.First(x => x.Kind == LegacyItemKind.Tax);
            invalid = invalid with
            {
                Input = invalid.Input with { Tax = HouseholdCostCategoryInput.FromItems([
                    new(source.Key, "Original", SensitivityValue.Constant(2400), HouseholdCostCadence.Annual),
                    new("copied-tax", "Copy", SensitivityValue.Constant(2400), HouseholdCostCadence.Annual)]) },
                LegacyDecisions = decisions.Select(x => x.Key == source.Key
                    ? x with { Disposition = LegacyItemDisposition.Map, TargetKey = "copied-tax" } : x).ToArray(),
            };
        }
        writes[1] = writes[1] with { Cost = invalid };
        await Assert.ThrowsAsync<HouseholdStoreException>(() => Transition(db).ConfirmAsync(Profile(), 0, review.Revision, writes));
        Assert.Equal(2, await CountAsync("saved_cost_scenarios"));
        Assert.Equal(0, await CountAsync("vehicle_cost_inputs"));
        Assert.Equal(0, (await new HouseholdProfileStore(db).GetAsync()).Revision);
        Assert.All((await Transition(db).GetAsync()).Vehicles, x => Assert.Equal(1, x.Revision));
    }

    [Fact]
    public async Task Listing_or_legacy_changes_invalidate_a_previously_read_confirmation()
    {
        await Fixture.ResetDatabaseAsync();
        await using var first = Fixture.CreateDbContext();
        await using var second = Fixture.CreateDbContext();
        var legacy = await Legacy(first).CreateAsync(Reg(), ScenarioFactory.Complete());
        var old = await Transition(first).GetAsync();
        await Listings(second).ReplaceAsync(legacy.VehicleId, 1, ListingFactory.ManualOnly());
        var conflict = await ErrorAsync("transitionRevisionConflict", () => Transition(first).ConfirmAsync(new(), 0, old.Revision, KeepAll(old)));
        Assert.Equal(2, conflict.ActualRevision);
        var current = await Transition(first).GetAsync();
        await ErrorAsync("vehicleRevisionConflict", () => Transition(first).ConfirmAsync(new(), 0, current.Revision, KeepAll(old)));
        await ErrorAsync("profileRevisionConflict", () => Transition(first).ConfirmAsync(new(), 1, current.Revision, KeepAll(current)));
        await ErrorAsync("transitionSetConflict", () => Transition(first).ConfirmAsync(new(), 0, current.Revision, []));
        await Legacy(second).ReplaceAsync(legacy.VehicleId, 2, ScenarioFactory.Replacement(), SavedScenarioListingLinkMode.Preserve);
        Assert.Equal(3, (await Transition(first).GetAsync()).Revision);
        await Legacy(second).DeleteAsync(legacy.VehicleId, 3);
        Assert.Equal(4, (await Transition(first).GetAsync()).Revision);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(999)]
    public async Task Broken_results_never_block_input_recovery_or_confirmation(int resultVersion)
    {
        await Fixture.ResetDatabaseAsync();
        await using var db = Fixture.CreateDbContext();
        await Legacy(db).CreateAsync(Reg(), ScenarioFactory.Complete());
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE saved_cost_scenarios SET result_schema_version = {resultVersion}, result_snapshot = '{{}}'::jsonb");
        var review = await Transition(db).GetAsync();
        Assert.Equal(ScenarioFactory.Complete().PurchasePriceSek, Assert.Single(review.Vehicles).Legacy!.Input.PurchasePriceSek);
        await Transition(db).ConfirmAsync(new(), 0, review.Revision, KeepAll(review));
        Assert.Equal(0, await CountAsync("saved_cost_scenarios"));
        Assert.Equal(1, await CountAsync("vehicle_cost_inputs"));
    }

    [Fact]
    public async Task Database_failure_during_confirmation_preserves_profile_legacy_children_and_results()
    {
        await Fixture.ResetDatabaseAsync();
        await using var db = Fixture.CreateDbContext();
        await Legacy(db).CreateAsync(Reg(), ScenarioFactory.Complete());
        var review = await Transition(db).GetAsync();
        var previousResult = await ScalarAsync<string>("SELECT result_snapshot::text FROM saved_cost_scenarios");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE household_state ADD CONSTRAINT test_reject_profile CHECK (profile_revision = 0)");
        try
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => Transition(db).ConfirmAsync(Profile(), 0, review.Revision, KeepAll(review)));
            Assert.Equal(previousResult, await ScalarAsync<string>("SELECT result_snapshot::text FROM saved_cost_scenarios"));
            Assert.Equal(2, await CountAsync("scenario_energy_sources"));
            Assert.Equal(2, await CountAsync("scenario_recurring_costs"));
            Assert.Equal(2, await CountAsync("scenario_one_time_costs"));
            Assert.Equal(0, await CountAsync("vehicle_cost_inputs"));
            Assert.Equal(new SavedHouseholdProfile(null, 0), await new HouseholdProfileStore(db).GetAsync());
            Assert.Equal(review.Revision, (await Transition(db).GetAsync()).Revision);
        }
        finally { await db.Database.ExecuteSqlRawAsync("ALTER TABLE household_state DROP CONSTRAINT test_reject_profile"); }
    }
}
