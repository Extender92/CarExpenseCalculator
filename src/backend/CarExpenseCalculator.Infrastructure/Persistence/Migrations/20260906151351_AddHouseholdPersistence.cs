using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarExpenseCalculator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHouseholdPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "household_state",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    profile_revision = table.Column<long>(type: "bigint", nullable: false),
                    transition_revision = table.Column<long>(type: "bigint", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    profile = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_household_state", x => x.id);
                    table.CheckConstraint("ck_household_state_profile", "(profile IS NULL AND profile_revision = 0) OR (profile IS NOT NULL AND jsonb_typeof(profile) = 'object' AND profile_revision > 0)");
                    table.CheckConstraint("ck_household_state_revisions", "profile_revision >= 0 AND transition_revision >= 0 AND schema_version >= 1");
                    table.CheckConstraint("ck_household_state_singleton", "id = 1");
                });

            migrationBuilder.CreateTable(
                name: "vehicle_cost_inputs",
                columns: table => new
                {
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    input = table.Column<string>(type: "jsonb", nullable: false),
                    source_listing_version = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_cost_inputs", x => x.vehicle_id);
                    table.CheckConstraint("ck_vehicle_cost_inputs_payload", "jsonb_typeof(input) = 'object'");
                    table.CheckConstraint("ck_vehicle_cost_inputs_version", "schema_version >= 1 AND (source_listing_version IS NULL OR source_listing_version >= 1)");
                    table.ForeignKey(
                        name: "FK_vehicle_cost_inputs_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_draft",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    registration_number = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    base_vehicle_id = table.Column<Guid>(type: "uuid", nullable: true),
                    base_vehicle_revision = table.Column<long>(type: "bigint", nullable: true),
                    input = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_draft", x => x.id);
                    table.CheckConstraint("ck_vehicle_draft_base", "(base_vehicle_id IS NULL AND base_vehicle_revision IS NULL) OR (base_vehicle_id IS NOT NULL AND base_vehicle_revision IS NOT NULL AND base_vehicle_revision >= 1)");
                    table.CheckConstraint("ck_vehicle_draft_payload", "(input IS NULL AND registration_number IS NULL AND base_vehicle_id IS NULL AND base_vehicle_revision IS NULL) OR (input IS NOT NULL AND jsonb_typeof(input) = 'object' AND registration_number IS NOT NULL)");
                    table.CheckConstraint("ck_vehicle_draft_registration", "registration_number IS NULL OR registration_number ~ '^[A-HJ-PR-UW-Z]{3}[0-9]{2}([0-9]|[A-HJ-NPR-UW-Z])$'");
                    table.CheckConstraint("ck_vehicle_draft_revision", "revision >= 0 AND schema_version >= 1");
                    table.CheckConstraint("ck_vehicle_draft_singleton", "id = 1");
                    table.ForeignKey(
                        name: "FK_vehicle_draft_vehicles_base_vehicle_id",
                        column: x => x.base_vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "household_state",
                columns: new[] { "id", "profile", "profile_revision", "schema_version", "transition_revision" },
                values: new object[] { 1, null, 0L, 1, 0L });

            migrationBuilder.InsertData(
                table: "vehicle_draft",
                columns: new[] { "id", "base_vehicle_id", "base_vehicle_revision", "input", "registration_number", "revision", "schema_version" },
                values: new object[] { 1, null, null, null, null, 0L, 1 });

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_draft_base_vehicle_id",
                table: "vehicle_draft",
                column: "base_vehicle_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "household_state");

            migrationBuilder.DropTable(
                name: "vehicle_cost_inputs");

            migrationBuilder.DropTable(
                name: "vehicle_draft");

            // Explicit rollback discards household-only cars; do not reconstruct v1 inputs.
            migrationBuilder.Sql("""
                DELETE FROM vehicles AS vehicle
                WHERE NOT EXISTS (SELECT 1 FROM saved_cost_scenarios AS scenario WHERE scenario.vehicle_id = vehicle.id)
                  AND NOT EXISTS (SELECT 1 FROM vehicle_listings AS listing WHERE listing.vehicle_id = vehicle.id);
                """);
        }
    }
}
