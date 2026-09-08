using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarExpenseCalculator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddComparisonPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rule_profile",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    input = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rule_profile", x => x.id);
                    table.CheckConstraint("ck_rule_profile_payload", "(input IS NULL AND revision = 0) OR (input IS NOT NULL AND jsonb_typeof(input) = 'object' AND revision > 0)");
                    table.CheckConstraint("ck_rule_profile_singleton", "id = 1");
                    table.CheckConstraint("ck_rule_profile_version", "schema_version >= 1 AND revision >= 0");
                });

            migrationBuilder.CreateTable(
                name: "vehicle_comparison_facts",
                columns: table => new
                {
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    input = table.Column<string>(type: "jsonb", nullable: false),
                    reviewed_listing_version = table.Column<long>(type: "bigint", nullable: true),
                    cost_confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_comparison_facts", x => x.vehicle_id);
                    table.CheckConstraint("ck_vehicle_comparison_facts_payload", "jsonb_typeof(input) = 'object'");
                    table.CheckConstraint("ck_vehicle_comparison_facts_version", "schema_version >= 1 AND (reviewed_listing_version IS NULL OR reviewed_listing_version >= 1)");
                    table.ForeignKey(
                        name: "FK_vehicle_comparison_facts_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "rule_profile",
                columns: new[] { "id", "input", "revision", "schema_version" },
                values: new object[] { 1, null, 0L, 1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rule_profile");

            migrationBuilder.DropTable(
                name: "vehicle_comparison_facts");
        }
    }
}
