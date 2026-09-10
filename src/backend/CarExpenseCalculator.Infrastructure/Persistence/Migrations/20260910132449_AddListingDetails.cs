using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarExpenseCalculator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddListingDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint("ck_vehicle_listings_versions", "vehicle_listings");
            migrationBuilder.DropCheckConstraint("ck_vehicle_listings_extraction_metadata", "vehicle_listings");
            migrationBuilder.AddCheckConstraint("ck_vehicle_listings_versions", "vehicle_listings", "listing_version >= 1 AND listing_schema_version IN (1, 2)");
            migrationBuilder.AddCheckConstraint("ck_vehicle_listings_extraction_metadata", "vehicle_listings", "(requested_model IS NULL AND prompt_version IS NULL AND extraction_schema_version IS NULL) OR (requested_model IS NOT NULL AND length(btrim(requested_model)) BETWEEN 1 AND 100 AND prompt_version IS NOT NULL AND prompt_version IN (2, 3) AND extraction_schema_version IS NOT NULL AND extraction_schema_version = prompt_version)");
            migrationBuilder.AddColumn<string>(
                name: "details",
                table: "vehicle_listings",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM vehicle_listings WHERE listing_schema_version > 1 OR details IS NOT NULL)
                       OR EXISTS (SELECT 1 FROM vehicle_draft WHERE input #>> '{listing,extractionSchemaVersion}' = '3'
                           OR (input #> '{listing,values,details}' IS NOT NULL AND input #> '{listing,values,details}' <> 'null'::jsonb)) THEN
                        RAISE EXCEPTION 'New listing content cannot be downgraded. Restore a compatible backup; do not relabel extraction versions.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropCheckConstraint("ck_vehicle_listings_versions", "vehicle_listings");
            migrationBuilder.DropCheckConstraint("ck_vehicle_listings_extraction_metadata", "vehicle_listings");
            migrationBuilder.AddCheckConstraint("ck_vehicle_listings_versions", "vehicle_listings", "listing_version >= 1 AND listing_schema_version = 1");
            migrationBuilder.AddCheckConstraint("ck_vehicle_listings_extraction_metadata", "vehicle_listings", "(requested_model IS NULL AND prompt_version IS NULL AND extraction_schema_version IS NULL) OR (requested_model IS NOT NULL AND length(btrim(requested_model)) BETWEEN 1 AND 100 AND prompt_version IS NOT NULL AND prompt_version = 2 AND extraction_schema_version IS NOT NULL AND extraction_schema_version = 2)");
            migrationBuilder.DropColumn(
                name: "details",
                table: "vehicle_listings");
        }
    }
}
