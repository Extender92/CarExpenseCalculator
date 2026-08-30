using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarExpenseCalculator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSavedCostScenarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vehicles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    registration_number = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    vehicle_label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vehicles", x => x.id);
                    table.CheckConstraint("ck_vehicles_registration_number", "registration_number ~ '^[A-HJ-PR-UW-Z]{3}[0-9]{2}([0-9]|[A-HJ-NPR-UW-Z])$'");
                    table.CheckConstraint("ck_vehicles_revision", "revision >= 1");
                });

            migrationBuilder.CreateTable(
                name: "saved_cost_scenarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    calculation_period_months = table.Column<int>(type: "integer", nullable: false),
                    purchase_price_sek = table.Column<decimal>(type: "numeric", nullable: false),
                    expected_residual_value_sek = table.Column<decimal>(type: "numeric", nullable: true),
                    annual_distance_kilometres = table.Column<decimal>(type: "numeric", nullable: false),
                    financing_down_payment_sek = table.Column<decimal>(type: "numeric", nullable: true),
                    financing_annual_nominal_interest_rate_percent = table.Column<decimal>(type: "numeric", nullable: true),
                    financing_term_months = table.Column<int>(type: "integer", nullable: true),
                    vehicle_tax_amount_sek = table.Column<decimal>(type: "numeric", nullable: true),
                    vehicle_tax_cadence = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    insurance_amount_sek = table.Column<decimal>(type: "numeric", nullable: true),
                    insurance_cadence = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    maintenance_and_repairs_amount_sek = table.Column<decimal>(type: "numeric", nullable: true),
                    maintenance_and_repairs_cadence = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    calculation_version = table.Column<int>(type: "integer", nullable: false),
                    result_schema_version = table.Column<int>(type: "integer", nullable: false),
                    result_snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    calculated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_saved_cost_scenarios", x => x.id);
                    table.CheckConstraint("ck_saved_cost_scenarios_annual_distance", "annual_distance_kilometres BETWEEN 0 AND 1000000");
                    table.CheckConstraint("ck_saved_cost_scenarios_financing_presence", "(financing_down_payment_sek IS NULL AND financing_annual_nominal_interest_rate_percent IS NULL AND financing_term_months IS NULL) OR (financing_down_payment_sek IS NOT NULL AND financing_annual_nominal_interest_rate_percent IS NOT NULL AND financing_term_months IS NOT NULL)");
                    table.CheckConstraint("ck_saved_cost_scenarios_financing_values", "financing_down_payment_sek IS NULL OR (purchase_price_sek > 0 AND financing_down_payment_sek >= 0 AND financing_down_payment_sek < purchase_price_sek AND financing_annual_nominal_interest_rate_percent BETWEEN 0 AND 100 AND financing_term_months BETWEEN 1 AND 120)");
                    table.CheckConstraint("ck_saved_cost_scenarios_insurance_amount", "insurance_amount_sek IS NULL OR insurance_amount_sek BETWEEN 0 AND 100000000");
                    table.CheckConstraint("ck_saved_cost_scenarios_insurance_presence", "(insurance_amount_sek IS NULL) = (insurance_cadence IS NULL)");
                    table.CheckConstraint("ck_saved_cost_scenarios_maintenance_amount", "maintenance_and_repairs_amount_sek IS NULL OR maintenance_and_repairs_amount_sek BETWEEN 0 AND 100000000");
                    table.CheckConstraint("ck_saved_cost_scenarios_maintenance_presence", "(maintenance_and_repairs_amount_sek IS NULL) = (maintenance_and_repairs_cadence IS NULL)");
                    table.CheckConstraint("ck_saved_cost_scenarios_period", "calculation_period_months BETWEEN 1 AND 120");
                    table.CheckConstraint("ck_saved_cost_scenarios_purchase_price", "purchase_price_sek BETWEEN 0 AND 100000000");
                    table.CheckConstraint("ck_saved_cost_scenarios_recurring_cadences", "(vehicle_tax_cadence IS NULL OR vehicle_tax_cadence IN ('Monthly', 'Annual')) AND (insurance_cadence IS NULL OR insurance_cadence IN ('Monthly', 'Annual')) AND (maintenance_and_repairs_cadence IS NULL OR maintenance_and_repairs_cadence IN ('Monthly', 'Annual'))");
                    table.CheckConstraint("ck_saved_cost_scenarios_residual_value", "expected_residual_value_sek IS NULL OR (expected_residual_value_sek BETWEEN 0 AND purchase_price_sek)");
                    table.CheckConstraint("ck_saved_cost_scenarios_vehicle_tax_amount", "vehicle_tax_amount_sek IS NULL OR vehicle_tax_amount_sek BETWEEN 0 AND 100000000");
                    table.CheckConstraint("ck_saved_cost_scenarios_vehicle_tax_presence", "(vehicle_tax_amount_sek IS NULL) = (vehicle_tax_cadence IS NULL)");
                    table.CheckConstraint("ck_saved_cost_scenarios_versions", "calculation_version >= 1 AND result_schema_version >= 1");
                    table.ForeignKey(
                        name: "fk_saved_cost_scenarios_vehicles",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scenario_energy_sources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scenario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    consumption_per_100_kilometres = table.Column<decimal>(type: "numeric", nullable: false),
                    price_per_unit_sek = table.Column<decimal>(type: "numeric", nullable: false),
                    distance_share_percent = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scenario_energy_sources", x => x.id);
                    table.CheckConstraint("ck_scenario_energy_sources_consumption", "consumption_per_100_kilometres > 0 AND consumption_per_100_kilometres <= 10000");
                    table.CheckConstraint("ck_scenario_energy_sources_position", "position BETWEEN 0 AND 1");
                    table.CheckConstraint("ck_scenario_energy_sources_price", "price_per_unit_sek BETWEEN 0 AND 100000");
                    table.CheckConstraint("ck_scenario_energy_sources_share", "distance_share_percent > 0 AND distance_share_percent <= 100");
                    table.CheckConstraint("ck_scenario_energy_sources_unit", "unit IN ('Litre', 'KilowattHour', 'Kilogram')");
                    table.ForeignKey(
                        name: "fk_scenario_energy_sources_scenarios",
                        column: x => x.scenario_id,
                        principalTable: "saved_cost_scenarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scenario_one_time_costs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scenario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    amount_sek = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scenario_one_time_costs", x => x.id);
                    table.CheckConstraint("ck_scenario_one_time_costs_amount", "amount_sek BETWEEN 0 AND 100000000");
                    table.CheckConstraint("ck_scenario_one_time_costs_position", "position BETWEEN 0 AND 49");
                    table.ForeignKey(
                        name: "fk_scenario_one_time_costs_scenarios",
                        column: x => x.scenario_id,
                        principalTable: "saved_cost_scenarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scenario_recurring_costs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scenario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    amount_sek = table.Column<decimal>(type: "numeric", nullable: false),
                    cadence = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scenario_recurring_costs", x => x.id);
                    table.CheckConstraint("ck_scenario_recurring_costs_amount", "amount_sek BETWEEN 0 AND 100000000");
                    table.CheckConstraint("ck_scenario_recurring_costs_cadence", "cadence IN ('Monthly', 'Annual')");
                    table.CheckConstraint("ck_scenario_recurring_costs_position", "position BETWEEN 0 AND 49");
                    table.ForeignKey(
                        name: "fk_scenario_recurring_costs_scenarios",
                        column: x => x.scenario_id,
                        principalTable: "saved_cost_scenarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_saved_cost_scenarios_vehicle_id",
                table: "saved_cost_scenarios",
                column: "vehicle_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_scenario_energy_sources_scenario_id_position",
                table: "scenario_energy_sources",
                columns: new[] { "scenario_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_scenario_one_time_costs_scenario_id_position",
                table: "scenario_one_time_costs",
                columns: new[] { "scenario_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_scenario_recurring_costs_scenario_id_position",
                table: "scenario_recurring_costs",
                columns: new[] { "scenario_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vehicles_updated_at_utc_id",
                table: "vehicles",
                columns: new[] { "updated_at_utc", "id" });

            migrationBuilder.CreateIndex(
                name: "ux_vehicles_registration_number",
                table: "vehicles",
                column: "registration_number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "scenario_energy_sources");

            migrationBuilder.DropTable(
                name: "scenario_one_time_costs");

            migrationBuilder.DropTable(
                name: "scenario_recurring_costs");

            migrationBuilder.DropTable(
                name: "saved_cost_scenarios");

            migrationBuilder.DropTable(
                name: "vehicles");
        }
    }
}
