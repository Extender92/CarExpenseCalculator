using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CarExpenseCalculator.Infrastructure.Persistence.Vehicles;

namespace CarExpenseCalculator.Infrastructure.Persistence.SavedCostScenarios;

internal sealed class VehicleEntityConfiguration : IEntityTypeConfiguration<VehicleEntity>
{
    public void Configure(EntityTypeBuilder<VehicleEntity> builder)
    {
        builder.ToTable(
            "vehicles",
            table =>
            {
                table.HasCheckConstraint("ck_vehicles_revision", "revision >= 1");
                table.HasCheckConstraint(
                    "ck_vehicles_registration_number",
                    "registration_number ~ '^[A-HJ-PR-UW-Z]{3}[0-9]{2}([0-9]|[A-HJ-NPR-UW-Z])$'");
            });
        builder.HasKey(entity => entity.Id).HasName("pk_vehicles");
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(entity => entity.RegistrationNumber)
            .HasColumnName("registration_number")
            .HasMaxLength(6)
            .IsRequired();
        builder.Property(entity => entity.VehicleLabel)
            .HasColumnName("vehicle_label")
            .HasMaxLength(120);
        builder.Property(entity => entity.Revision)
            .HasColumnName("revision")
            .IsConcurrencyToken();
        builder.Property(entity => entity.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone");
        builder.Property(entity => entity.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .HasColumnType("timestamp with time zone");
        builder.HasIndex(entity => entity.RegistrationNumber)
            .IsUnique()
            .HasDatabaseName("ux_vehicles_registration_number");
        builder.HasIndex(entity => new { entity.UpdatedAtUtc, entity.Id })
            .HasDatabaseName("ix_vehicles_updated_at_utc_id");
    }
}

internal sealed class SavedCostScenarioEntityConfiguration
    : IEntityTypeConfiguration<SavedCostScenarioEntity>
{
    public void Configure(EntityTypeBuilder<SavedCostScenarioEntity> builder)
    {
        builder.ToTable(
            "saved_cost_scenarios",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_saved_cost_scenarios_period",
                    "calculation_period_months BETWEEN 1 AND 120");
                table.HasCheckConstraint(
                    "ck_saved_cost_scenarios_purchase_price",
                    "purchase_price_sek BETWEEN 0 AND 100000000");
                table.HasCheckConstraint(
                    "ck_saved_cost_scenarios_residual_value",
                    "expected_residual_value_sek IS NULL OR (expected_residual_value_sek BETWEEN 0 AND purchase_price_sek)");
                table.HasCheckConstraint(
                    "ck_saved_cost_scenarios_annual_distance",
                    "annual_distance_kilometres BETWEEN 0 AND 1000000");
                table.HasCheckConstraint(
                    "ck_saved_cost_scenarios_financing_presence",
                    "(financing_down_payment_sek IS NULL AND financing_annual_nominal_interest_rate_percent IS NULL AND financing_term_months IS NULL) OR (financing_down_payment_sek IS NOT NULL AND financing_annual_nominal_interest_rate_percent IS NOT NULL AND financing_term_months IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_saved_cost_scenarios_financing_values",
                    "financing_down_payment_sek IS NULL OR (purchase_price_sek > 0 AND financing_down_payment_sek >= 0 AND financing_down_payment_sek < purchase_price_sek AND financing_annual_nominal_interest_rate_percent BETWEEN 0 AND 100 AND financing_term_months BETWEEN 1 AND 120)");
                table.HasCheckConstraint(
                    "ck_saved_cost_scenarios_vehicle_tax_presence",
                    "(vehicle_tax_amount_sek IS NULL) = (vehicle_tax_cadence IS NULL)");
                table.HasCheckConstraint(
                    "ck_saved_cost_scenarios_vehicle_tax_amount",
                    "vehicle_tax_amount_sek IS NULL OR vehicle_tax_amount_sek BETWEEN 0 AND 100000000");
                table.HasCheckConstraint(
                    "ck_saved_cost_scenarios_insurance_presence",
                    "(insurance_amount_sek IS NULL) = (insurance_cadence IS NULL)");
                table.HasCheckConstraint(
                    "ck_saved_cost_scenarios_insurance_amount",
                    "insurance_amount_sek IS NULL OR insurance_amount_sek BETWEEN 0 AND 100000000");
                table.HasCheckConstraint(
                    "ck_saved_cost_scenarios_maintenance_presence",
                    "(maintenance_and_repairs_amount_sek IS NULL) = (maintenance_and_repairs_cadence IS NULL)");
                table.HasCheckConstraint(
                    "ck_saved_cost_scenarios_maintenance_amount",
                    "maintenance_and_repairs_amount_sek IS NULL OR maintenance_and_repairs_amount_sek BETWEEN 0 AND 100000000");
                table.HasCheckConstraint(
                    "ck_saved_cost_scenarios_recurring_cadences",
                    "(vehicle_tax_cadence IS NULL OR vehicle_tax_cadence IN ('Monthly', 'Annual')) AND (insurance_cadence IS NULL OR insurance_cadence IN ('Monthly', 'Annual')) AND (maintenance_and_repairs_cadence IS NULL OR maintenance_and_repairs_cadence IN ('Monthly', 'Annual'))");
                table.HasCheckConstraint(
                    "ck_saved_cost_scenarios_versions",
                    "calculation_version >= 1 AND result_schema_version >= 1");
                table.HasCheckConstraint(
                    "ck_saved_cost_scenarios_source_listing_version",
                    "source_listing_version IS NULL OR source_listing_version >= 1");
            });

        builder.HasKey(entity => entity.Id).HasName("pk_saved_cost_scenarios");
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(entity => entity.VehicleId).HasColumnName("vehicle_id");
        builder.Property(entity => entity.CalculationPeriodMonths).HasColumnName("calculation_period_months");
        Numeric(builder.Property(entity => entity.PurchasePriceSek), "purchase_price_sek");
        Numeric(builder.Property(entity => entity.ExpectedResidualValueSek), "expected_residual_value_sek");
        Numeric(builder.Property(entity => entity.AnnualDistanceKilometres), "annual_distance_kilometres");
        Numeric(builder.Property(entity => entity.FinancingDownPaymentSek), "financing_down_payment_sek");
        Numeric(
            builder.Property(entity => entity.FinancingAnnualNominalInterestRatePercent),
            "financing_annual_nominal_interest_rate_percent");
        builder.Property(entity => entity.FinancingTermMonths).HasColumnName("financing_term_months");
        Numeric(builder.Property(entity => entity.VehicleTaxAmountSek), "vehicle_tax_amount_sek");
        builder.Property(entity => entity.VehicleTaxCadence)
            .HasColumnName("vehicle_tax_cadence")
            .HasConversion<string>()
            .HasMaxLength(16);
        Numeric(builder.Property(entity => entity.InsuranceAmountSek), "insurance_amount_sek");
        builder.Property(entity => entity.InsuranceCadence)
            .HasColumnName("insurance_cadence")
            .HasConversion<string>()
            .HasMaxLength(16);
        Numeric(
            builder.Property(entity => entity.MaintenanceAndRepairsAmountSek),
            "maintenance_and_repairs_amount_sek");
        builder.Property(entity => entity.MaintenanceAndRepairsCadence)
            .HasColumnName("maintenance_and_repairs_cadence")
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.Property(entity => entity.CalculationVersion).HasColumnName("calculation_version");
        builder.Property(entity => entity.ResultSchemaVersion).HasColumnName("result_schema_version");
        builder.Property(entity => entity.SourceListingVersion).HasColumnName("source_listing_version");
        builder.Property(entity => entity.ResultSnapshotJson)
            .HasColumnName("result_snapshot")
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(entity => entity.CalculatedAtUtc)
            .HasColumnName("calculated_at_utc")
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(entity => entity.VehicleId)
            .IsUnique()
            .HasDatabaseName("ux_saved_cost_scenarios_vehicle_id");
        builder.HasOne(entity => entity.Vehicle)
            .WithOne(entity => entity.Scenario)
            .HasForeignKey<SavedCostScenarioEntity>(entity => entity.VehicleId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_saved_cost_scenarios_vehicles");
    }

    private static void Numeric<TProperty>(
        PropertyBuilder<TProperty> property,
        string columnName)
    {
        property.HasColumnName(columnName).HasColumnType("numeric");
    }
}

internal sealed class ScenarioEnergySourceEntityConfiguration
    : IEntityTypeConfiguration<ScenarioEnergySourceEntity>
{
    public void Configure(EntityTypeBuilder<ScenarioEnergySourceEntity> builder)
    {
        builder.ToTable(
            "scenario_energy_sources",
            table =>
            {
                table.HasCheckConstraint("ck_scenario_energy_sources_position", "position BETWEEN 0 AND 1");
                table.HasCheckConstraint(
                    "ck_scenario_energy_sources_unit",
                    "unit IN ('Litre', 'KilowattHour', 'Kilogram')");
                table.HasCheckConstraint(
                    "ck_scenario_energy_sources_consumption",
                    "consumption_per_100_kilometres > 0 AND consumption_per_100_kilometres <= 10000");
                table.HasCheckConstraint(
                    "ck_scenario_energy_sources_price",
                    "price_per_unit_sek BETWEEN 0 AND 100000");
                table.HasCheckConstraint(
                    "ck_scenario_energy_sources_share",
                    "distance_share_percent > 0 AND distance_share_percent <= 100");
            });
        builder.HasKey(entity => entity.Id).HasName("pk_scenario_energy_sources");
        ConfigurePositionedChild(builder, "scenario_energy_sources");
        builder.Property(entity => entity.Label).HasColumnName("label").HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.Unit)
            .HasColumnName("unit")
            .HasConversion<string>()
            .HasMaxLength(32);
        Numeric(builder.Property(entity => entity.ConsumptionPer100Kilometres), "consumption_per_100_kilometres");
        Numeric(builder.Property(entity => entity.PricePerUnitSek), "price_per_unit_sek");
        Numeric(builder.Property(entity => entity.DistanceSharePercent), "distance_share_percent");
        builder.HasOne(entity => entity.Scenario)
            .WithMany(entity => entity.EnergySources)
            .HasForeignKey(entity => entity.ScenarioId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_scenario_energy_sources_scenarios");
    }

    private static void Numeric<TProperty>(PropertyBuilder<TProperty> property, string columnName) =>
        property.HasColumnName(columnName).HasColumnType("numeric");

    private static void ConfigurePositionedChild<T>(EntityTypeBuilder<T> builder, string tableName)
        where T : class
    {
        builder.Property<Guid>("Id").HasColumnName("id").ValueGeneratedNever();
        builder.Property<Guid>("ScenarioId").HasColumnName("scenario_id");
        builder.Property<int>("Position").HasColumnName("position");
        builder.HasIndex("ScenarioId", "Position")
            .IsUnique()
            .HasDatabaseName($"ux_{tableName}_scenario_id_position");
    }
}

internal sealed class ScenarioRecurringCostEntityConfiguration
    : IEntityTypeConfiguration<ScenarioRecurringCostEntity>
{
    public void Configure(EntityTypeBuilder<ScenarioRecurringCostEntity> builder)
    {
        builder.ToTable(
            "scenario_recurring_costs",
            table =>
            {
                table.HasCheckConstraint("ck_scenario_recurring_costs_position", "position BETWEEN 0 AND 49");
                table.HasCheckConstraint(
                    "ck_scenario_recurring_costs_amount",
                    "amount_sek BETWEEN 0 AND 100000000");
                table.HasCheckConstraint(
                    "ck_scenario_recurring_costs_cadence",
                    "cadence IN ('Monthly', 'Annual')");
            });
        builder.HasKey(entity => entity.Id).HasName("pk_scenario_recurring_costs");
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(entity => entity.ScenarioId).HasColumnName("scenario_id");
        builder.Property(entity => entity.Position).HasColumnName("position");
        builder.Property(entity => entity.Label).HasColumnName("label").HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.AmountSek).HasColumnName("amount_sek").HasColumnType("numeric");
        builder.Property(entity => entity.Cadence)
            .HasColumnName("cadence")
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.HasIndex(entity => new { entity.ScenarioId, entity.Position })
            .IsUnique()
            .HasDatabaseName("ux_scenario_recurring_costs_scenario_id_position");
        builder.HasOne(entity => entity.Scenario)
            .WithMany(entity => entity.OtherRecurringCosts)
            .HasForeignKey(entity => entity.ScenarioId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_scenario_recurring_costs_scenarios");
    }
}

internal sealed class ScenarioOneTimeCostEntityConfiguration
    : IEntityTypeConfiguration<ScenarioOneTimeCostEntity>
{
    public void Configure(EntityTypeBuilder<ScenarioOneTimeCostEntity> builder)
    {
        builder.ToTable(
            "scenario_one_time_costs",
            table =>
            {
                table.HasCheckConstraint("ck_scenario_one_time_costs_position", "position BETWEEN 0 AND 49");
                table.HasCheckConstraint(
                    "ck_scenario_one_time_costs_amount",
                    "amount_sek BETWEEN 0 AND 100000000");
            });
        builder.HasKey(entity => entity.Id).HasName("pk_scenario_one_time_costs");
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(entity => entity.ScenarioId).HasColumnName("scenario_id");
        builder.Property(entity => entity.Position).HasColumnName("position");
        builder.Property(entity => entity.Label).HasColumnName("label").HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.AmountSek).HasColumnName("amount_sek").HasColumnType("numeric");
        builder.HasIndex(entity => new { entity.ScenarioId, entity.Position })
            .IsUnique()
            .HasDatabaseName("ux_scenario_one_time_costs_scenario_id_position");
        builder.HasOne(entity => entity.Scenario)
            .WithMany(entity => entity.OtherOneTimeCosts)
            .HasForeignKey(entity => entity.ScenarioId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_scenario_one_time_costs_scenarios");
    }
}
