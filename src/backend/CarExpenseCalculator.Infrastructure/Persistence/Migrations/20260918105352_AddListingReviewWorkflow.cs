using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarExpenseCalculator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddListingReviewWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_vehicle_listings_versions",
                table: "vehicle_listings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_vehicle_draft_revision",
                table: "vehicle_draft");

            migrationBuilder.DropCheckConstraint(
                name: "ck_vehicle_cost_inputs_version",
                table: "vehicle_cost_inputs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_vehicle_comparison_facts_version",
                table: "vehicle_comparison_facts");

            migrationBuilder.CreateTable(
                name: "listing_review_drafts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    listing_reference = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    input = table.Column<string>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_listing_review_drafts", x => x.id);
                    table.CheckConstraint("ck_listing_review_drafts_dates", "updated_at_utc >= created_at_utc");
                    table.CheckConstraint("ck_listing_review_drafts_payload", "jsonb_typeof(input) = 'object'");
                    table.CheckConstraint("ck_listing_review_drafts_version", "revision >= 1 AND schema_version = 1");
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_vehicle_listings_versions",
                table: "vehicle_listings",
                sql: "listing_version >= 1 AND listing_schema_version IN (1, 2, 3)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_vehicle_draft_revision",
                table: "vehicle_draft",
                sql: "revision >= 0 AND schema_version IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_vehicle_cost_inputs_version",
                table: "vehicle_cost_inputs",
                sql: "schema_version IN (1, 2) AND (source_listing_version IS NULL OR source_listing_version >= 1)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_vehicle_comparison_facts_version",
                table: "vehicle_comparison_facts",
                sql: "schema_version IN (1, 2) AND (reviewed_listing_version IS NULL OR reviewed_listing_version >= 1)");

            migrationBuilder.CreateIndex(
                name: "IX_listing_review_drafts_listing_reference",
                table: "listing_review_drafts",
                column: "listing_reference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM listing_review_drafts)
                        OR EXISTS (SELECT 1 FROM vehicle_listings WHERE listing_schema_version > 2)
                        OR EXISTS (SELECT 1 FROM vehicle_cost_inputs WHERE schema_version > 1)
                        OR EXISTS (SELECT 1 FROM vehicle_comparison_facts WHERE schema_version > 1)
                        OR EXISTS (SELECT 1 FROM vehicle_draft WHERE input IS NOT NULL AND schema_version > 1)
                    THEN
                        RAISE EXCEPTION 'Cannot downgrade listing review workflow while drafts or new storage formats remain. Restore a compatible backup; never relabel stored data.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropTable(
                name: "listing_review_drafts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_vehicle_listings_versions",
                table: "vehicle_listings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_vehicle_draft_revision",
                table: "vehicle_draft");

            migrationBuilder.DropCheckConstraint(
                name: "ck_vehicle_cost_inputs_version",
                table: "vehicle_cost_inputs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_vehicle_comparison_facts_version",
                table: "vehicle_comparison_facts");

            migrationBuilder.AddCheckConstraint(
                name: "ck_vehicle_listings_versions",
                table: "vehicle_listings",
                sql: "listing_version >= 1 AND listing_schema_version IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_vehicle_draft_revision",
                table: "vehicle_draft",
                sql: "revision >= 0 AND schema_version >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_vehicle_cost_inputs_version",
                table: "vehicle_cost_inputs",
                sql: "schema_version >= 1 AND (source_listing_version IS NULL OR source_listing_version >= 1)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_vehicle_comparison_facts_version",
                table: "vehicle_comparison_facts",
                sql: "schema_version >= 1 AND (reviewed_listing_version IS NULL OR reviewed_listing_version >= 1)");
        }
    }
}
