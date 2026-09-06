using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Core.Households;
using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Core.Vehicles;
using CarExpenseCalculator.Infrastructure.Persistence;
using CarExpenseCalculator.Infrastructure.Persistence.Households;
using CarExpenseCalculator.Infrastructure.Persistence.SavedCostScenarios;
using CarExpenseCalculator.Infrastructure.Persistence.SavedListings;
using Npgsql;
using Xunit;

namespace CarExpenseCalculator.Infrastructure.IntegrationTests;

public abstract class HouseholdTestSupport(PostgreSqlFixture fixture)
{
    protected PostgreSqlFixture Fixture { get; } = fixture;
    protected static RegistrationNumber Reg(string value = "ABC123") => RegistrationNumber.Parse(value);
    protected static VehicleCostInputStore Costs(CarExpenseDbContext db) => new(db, TimeProvider.System);
    protected static SharedVehicleDraftStore Drafts(CarExpenseDbContext db) => new(db, new ListingDraftProcessor(), TimeProvider.System);
    protected static SavedListingStore Listings(CarExpenseDbContext db) => new(db, new ListingDraftProcessor(), TimeProvider.System);
    protected static SavedCostScenarioStore Legacy(CarExpenseDbContext db) => new(db, new CostScenarioCalculator(), TimeProvider.System);
    protected static HouseholdTransitionStore Transition(CarExpenseDbContext db) => new(db, TimeProvider.System);
    protected static VehicleCostWrite Write(decimal? price = 123_456.1234567890123456789012m) => new(new("temporary-candidate-key", price));
    protected static HouseholdProfileInput Profile() => new([new(FuelType.Biogas, EnergyUnit.Kilogram, SensitivityValue.Scenarios(12, 14, 17))])
    {
        StartMonth = new(2026, 11), PeriodMonths = 24, AnnualDistanceKilometres = 15_000.123456789012345678901234m,
        PurchaseCashSek = 30_000.123456789012345678901234m, StartupBudgetSek = 0, MonthlyBudgetSek = 5000,
        LoanTerms = new() { AnnualNominalInterestRatePercent = SensitivityValue.Scenarios(3, 5, 7), TermMonths = 60,
            SetupFeeSek = 499.12345678901234567890123456m, MonthlyFeeSek = 19 },
        ElectricDrivingSharePercent = SensitivityValue.Scenarios(80, 60, 40), HomeChargingSharePercent = SensitivityValue.Constant(70),
        HomeChargingPricePerKilowattHourSek = SensitivityValue.Constant(1.2345678901234567890123456789m),
        PublicChargingPricePerKilowattHourSek = SensitivityValue.Scenarios(3, 4, 5), ChargingLossPercent = SensitivityValue.Constant(10),
        ActiveSensitivityMode = SensitivityMode.Cautious,
    };
    protected static VehicleCostWrite KeepAll(SavedVehicleCostInput vehicle) => new(
        new(vehicle.RegistrationNumber.Value, vehicle.Legacy!.Input.PurchasePriceSek)
        { Residual = vehicle.Legacy.SuggestedInput.Residual }, vehicle.VehicleLabel,
        LegacyDecisions: vehicle.Legacy.Items.Select(x => new LegacyItemDecision(x.Key, LegacyItemDisposition.KeepForReview)).ToArray());
    protected static VehicleTransitionWrite[] KeepAll(HouseholdTransition transition) => transition.Vehicles
        .Select(x => new VehicleTransitionWrite(x.VehicleId, x.Revision, KeepAll(x))).ToArray();
    protected async Task<long> CountAsync(string table) => await ScalarAsync<long>($"SELECT count(*) FROM {table}");
    protected async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync())!;
    }
    protected static async Task<HouseholdStoreException> ErrorAsync(string code, Func<Task> action)
    {
        var error = await Assert.ThrowsAsync<HouseholdStoreException>(action);
        Assert.Equal(code, error.Code);
        return error;
    }
}
