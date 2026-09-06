using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Core.Households;
using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Infrastructure.Persistence.Households;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CarExpenseCalculator.Infrastructure.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class HouseholdCostStoreTests(PostgreSqlFixture fixture) : HouseholdTestSupport(fixture)
{
    [Fact]
    public async Task Profile_starts_absent_and_round_trips_all_fields_without_rounding()
    {
        await Fixture.ResetDatabaseAsync();
        await using var first = Fixture.CreateDbContext();
        var store = new HouseholdProfileStore(first);
        Assert.Equal(new SavedHouseholdProfile(null, 0), await store.GetAsync());
        var input = Profile();
        Assert.Equal(1, (await store.SaveAsync(input, 0)).Revision);
        await using var second = Fixture.CreateDbContext();
        var read = (await new HouseholdProfileStore(second).GetAsync()).Input!;
        Assert.Equal(input.PurchaseCashSek, read.PurchaseCashSek);
        Assert.Equal(input.AnnualDistanceKilometres, read.AnnualDistanceKilometres);
        Assert.Equal(input.StartMonth, read.StartMonth);
        Assert.Equal(input.PeriodMonths, read.PeriodMonths);
        Assert.Equal(input.LoanTerms, read.LoanTerms);
        Assert.Equal(input.EnergyPrices, read.EnergyPrices);
        Assert.Equal(input.ElectricDrivingSharePercent, read.ElectricDrivingSharePercent);
        Assert.Equal(input.HomeChargingSharePercent, read.HomeChargingSharePercent);
        Assert.Equal(input.HomeChargingPricePerKilowattHourSek, read.HomeChargingPricePerKilowattHourSek);
        Assert.Equal(input.PublicChargingPricePerKilowattHourSek, read.PublicChargingPricePerKilowattHourSek);
        Assert.Equal(input.ChargingLossPercent, read.ChargingLossPercent);
        Assert.Equal(input.ActiveSensitivityMode, read.ActiveSensitivityMode);
        Assert.Equal(input.StartupBudgetSek, read.StartupBudgetSek);
        Assert.Equal(input.MonthlyBudgetSek, read.MonthlyBudgetSek);
        var replaced = await store.SaveAsync(new(), 1);
        Assert.Null(replaced.Input!.PeriodMonths);
        Assert.Null(replaced.Input.LoanTerms);
        Assert.Empty(replaced.Input.EnergyPrices);
        Assert.Equal(0, (await Transition(first).GetAsync()).Revision);
    }

    [Fact]
    public async Task Purchase_round_trips_unknown_zero_empty_included_evidence_modes_and_energy()
    {
        await Fixture.ResetDatabaseAsync();
        var item = new HouseholdCostItem("service-1", "Service", SensitivityValue.Scenarios(1.1234567890123456789012345678m, 0, 33),
            HouseholdCostCadence.Annual, DueMonthOfYear: 3, EvidenceNote: "Written quote", SourceUrl: "https://example.com/quote");
        var input = new VehicleCostInput("preview", 123_456.1234567890123456789012m,
            [new("gas", FuelType.Biogas, EnergyUnit.Kilogram, SensitivityValue.Constant(4.1234567890123456789012345678m), ConsumptionBasis.WholeDistance)])
        {
            Residual = HouseholdResidualInput.FixedAmount(SensitivityValue.Scenarios(100000, 90000, 80000), 24),
            Tax = null, Insurance = HouseholdCostCategoryInput.KnownZero(), Service = HouseholdCostCategoryInput.Included([item]),
            Repairs = HouseholdCostCategoryInput.FromItems([new("repair", "Repair", null, HouseholdCostCadence.Once, 0)]),
            AdditionalRepairAllowancePerMonthSek = SensitivityValue.Constant(0),
        };
        await using var first = Fixture.CreateDbContext();
        var created = await Costs(first).CreateAsync(Reg(" abc-12d "), new(input, " Volvo "));
        await using var second = Fixture.CreateDbContext();
        var read = (await Costs(second).GetByRegistrationNumberAsync(Reg("ABC12D")))!;
        Assert.Equal(created.VehicleId, read.VehicleId);
        Assert.Equal("ABC12D", read.Input!.CandidateKey);
        Assert.Equal("Volvo", read.VehicleLabel);
        Assert.Equal(input.PriceSek, read.Input.PriceSek);
        Assert.Equal(input.Residual, read.Input.Residual);
        Assert.Equal(input.EnergySources, read.Input.EnergySources);
        Assert.Null(read.Input.Tax);
        Assert.Empty(read.Input.Insurance!.Items);
        Assert.False(read.Input.Insurance.IsIncluded);
        Assert.True(read.Input.Service!.IsIncluded);
        Assert.Equal(item, Assert.Single(read.Input.Service.Items));
        Assert.Null(Assert.Single(read.Input.Repairs!.Items).AmountSek);
        Assert.Equal(0, read.Input.AdditionalRepairAllowancePerMonthSek!.Single);
        Assert.Null((await new HouseholdProfileStore(second).GetAsync()).Input);
        Assert.Equal(1, await ScalarAsync<int>("SELECT schema_version FROM vehicle_cost_inputs"));
        Assert.Equal(0, await CountAsync("saved_cost_scenarios"));
    }

    [Fact]
    public async Task Lease_full_replacement_retains_only_current_payload()
    {
        await Fixture.ResetDatabaseAsync();
        await using var db = Fixture.CreateDbContext();
        var created = await Costs(db).CreateAsync(Reg(), Write());
        var lease = new HouseholdLeaseInput([new(1, 1234.123456789012345678901234m), new(2, null)],
            [new("end", "End", SensitivityValue.Scenarios(0, 25, 99))],
            [new("extra", "Extra", null, null, "To be confirmed", "https://example.com/lease")])
        {
            TermMonths = 24, UpfrontNonRefundableSek = 500, RefundableDepositSek = 3000,
            DepositRefundSek = SensitivityValue.Scenarios(3000, 2000, 0), IncludedDistanceKilometres = 20000.123456789012345678901234m,
            ExcessDistancePricePerKilometreSek = SensitivityValue.Constant(2.1234567890123456789012345678m),
            PriceBasis = LeasePriceBasis.Estimated, EnergyIncluded = true,
        };
        await Costs(db).ReplaceAsync(created.VehicleId, 1, new(VehicleCostInput.ForLease("lease", lease, [])));
        await using var reader = Fixture.CreateDbContext();
        var read = (await Costs(reader).GetAsync(created.VehicleId))!;
        Assert.Equal(2, read.Revision);
        Assert.Null(read.Input!.PriceSek);
        Assert.Null(read.Input.Residual);
        Assert.Empty(read.Input.EnergySources!);
        var saved = read.Input.Lease!;
        Assert.Equal(lease.MonthlyPayments, saved.MonthlyPayments);
        Assert.Equal(lease.EndFees, saved.EndFees);
        Assert.Equal(lease.OtherPayments, saved.OtherPayments);
        Assert.Equal(lease.TermMonths, saved.TermMonths);
        Assert.Equal(lease.UpfrontNonRefundableSek, saved.UpfrontNonRefundableSek);
        Assert.Equal(lease.RefundableDepositSek, saved.RefundableDepositSek);
        Assert.Equal(lease.DepositRefundSek, saved.DepositRefundSek);
        Assert.Equal(lease.IncludedDistanceKilometres, saved.IncludedDistanceKilometres);
        Assert.Equal(lease.ExcessDistancePricePerKilometreSek, saved.ExcessDistancePricePerKilometreSek);
        Assert.Equal(lease.PriceBasis, saved.PriceBasis);
        Assert.True(saved.EnergyIncluded);
        Assert.Equal(1, await CountAsync("vehicle_cost_inputs"));
        var replaced = await Costs(db).ReplaceAsync(created.VehicleId, 2, Write(null));
        Assert.Null(replaced.Input!.Lease);
        Assert.Equal(0, await CountAsync("saved_cost_scenarios"));
    }

    [Fact]
    public async Task Invalid_supplied_values_in_inactive_modes_are_rejected_but_missing_fields_can_be_saved()
    {
        await Fixture.ResetDatabaseAsync();
        await using var db = Fixture.CreateDbContext();
        await Assert.ThrowsAsync<HouseholdInputValidationException>(() => new HouseholdProfileStore(db).SaveAsync(
            new() { PurchaseCashSek = -1 }, 0));
        await Assert.ThrowsAsync<HouseholdInputValidationException>(() => Costs(db).CreateAsync(Reg(), new(
            new("x", null) { AdditionalRepairAllowancePerMonthSek = SensitivityValue.Scenarios(0, 0, -1) })));
        await Assert.ThrowsAsync<HouseholdInputValidationException>(() => Costs(db).CreateAsync(Reg(), new(
            new("x", 1) { AcquisitionType = AcquisitionType.Lease, Lease = new() })));
        Assert.Equal(0, await CountAsync("vehicles"));
        Assert.Equal(0, (await new HouseholdProfileStore(db).GetAsync()).Revision);
        Assert.NotNull((await Costs(db).CreateAsync(Reg(), new(VehicleCostInput.ForLease("x", null)))).Input);
    }

    [Fact]
    public async Task List_includes_listing_only_legacy_and_current_without_loading_legacy_results()
    {
        await Fixture.ResetDatabaseAsync();
        await using var db = Fixture.CreateDbContext();
        await Listings(db).CreateAsync(Reg(), ListingFactory.ManualOnly());
        await Legacy(db).CreateAsync(Reg("DEF456"), ScenarioFactory.Complete());
        await Costs(db).CreateAsync(Reg("JKL789"), Write());
        var brokenResult = "{\"unreadable\":true}";
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE saved_cost_scenarios SET calculation_version = 999, result_schema_version = 999, result_snapshot = {brokenResult}::jsonb");
        var list = await Costs(db).ListAsync();
        Assert.Equal(new[] { VehicleInputState.ListingOnly, VehicleInputState.LegacyPending, VehicleInputState.Current }, list.Select(x => x.State));
        Assert.Equal(999, list[1].Legacy!.ResultSchemaVersion);
        Assert.Equal(ScenarioFactory.Complete().PurchasePriceSek, list[1].Legacy!.Input.PurchasePriceSek);
    }

    [Fact]
    public async Task Competing_first_saves_and_vehicle_writes_return_current_revision_conflicts()
    {
        await Fixture.ResetDatabaseAsync();
        await using var first = Fixture.CreateDbContext();
        await using var second = Fixture.CreateDbContext();
        var outcomes = await Task.WhenAll(Attempt(() => new HouseholdProfileStore(first).SaveAsync(Profile(), 0)),
            Attempt(() => new HouseholdProfileStore(second).SaveAsync(new(), 0)));
        Assert.Single(outcomes, x => x is null);
        var conflict = Assert.Single(outcomes.OfType<HouseholdStoreException>());
        Assert.Equal("profileRevisionConflict", conflict.Code);
        Assert.Equal(1, conflict.ActualRevision);
        var car = await Costs(first).CreateAsync(Reg(), Write());
        outcomes = await Task.WhenAll(Attempt(() => Costs(first).ReplaceAsync(car.VehicleId, 1, Write(1))),
            Attempt(() => Listings(second).ReplaceAsync(car.VehicleId, 1, ListingFactory.ManualOnly())));
        Assert.Single(outcomes, x => x is null);
        Assert.Single(outcomes, x => x is not null);
        Assert.Equal(2, (await Costs(first).GetAsync(car.VehicleId))!.Revision);
    }

    private static async Task<Exception?> Attempt(Func<Task> action)
    {
        try { await action(); return null; }
        catch (Exception exception) { return exception; }
    }
}
