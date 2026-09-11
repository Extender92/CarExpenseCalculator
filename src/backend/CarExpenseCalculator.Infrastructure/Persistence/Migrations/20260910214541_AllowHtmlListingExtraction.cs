using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarExpenseCalculator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowHtmlListingExtraction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_vehicle_listings_extraction_metadata",
                table: "vehicle_listings");

            migrationBuilder.AddCheckConstraint(
                name: "ck_vehicle_listings_extraction_metadata",
                table: "vehicle_listings",
                sql: "(requested_model IS NULL AND prompt_version IS NULL AND extraction_schema_version IS NULL) OR (requested_model IS NOT NULL AND length(btrim(requested_model)) BETWEEN 1 AND 100 AND prompt_version IS NOT NULL AND extraction_schema_version IS NOT NULL AND ((prompt_version = 2 AND extraction_schema_version = 2) OR (prompt_version IN (3, 4) AND extraction_schema_version = 3)))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM vehicle_listings WHERE prompt_version = 4
                        OR jsonb_path_exists(field_provenance, '$.**.extractionMethod ? (@ == "html")')
                        OR jsonb_path_exists(details, '$.**.extractionMethod ? (@ == "html")'))
                       OR EXISTS (SELECT 1 FROM vehicle_draft WHERE input #>> '{listing,promptVersion}' = '4'
                        OR jsonb_path_exists(input, '$.**.extractionMethod ? (@ == "html")'))
                       OR EXISTS (SELECT 1 FROM vehicle_comparison_facts
                        WHERE jsonb_path_exists(input, '$.**.extractionMethod ? (@ == "html")')) THEN
                        RAISE EXCEPTION 'HTML listing content cannot be downgraded. Restore a compatible backup; do not relabel metadata.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "ck_vehicle_listings_extraction_metadata",
                table: "vehicle_listings");

            migrationBuilder.AddCheckConstraint(
                name: "ck_vehicle_listings_extraction_metadata",
                table: "vehicle_listings",
                sql: "(requested_model IS NULL AND prompt_version IS NULL AND extraction_schema_version IS NULL) OR (requested_model IS NOT NULL AND length(btrim(requested_model)) BETWEEN 1 AND 100 AND prompt_version IS NOT NULL AND prompt_version IN (2, 3) AND extraction_schema_version IS NOT NULL AND extraction_schema_version = prompt_version)");
        }
    }
}
