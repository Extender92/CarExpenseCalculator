using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarExpenseCalculator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrentVehicleListings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vehicle_listings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    listing_version = table.Column<long>(type: "bigint", nullable: false),
                    listing_schema_version = table.Column<int>(type: "integer", nullable: false),
                    submitted_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    normalized_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    missing_fields = table.Column<string[]>(type: "text[]", nullable: false),
                    requested_model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    prompt_version = table.Column<int>(type: "integer", nullable: true),
                    extraction_schema_version = table.Column<int>(type: "integer", nullable: true),
                    analyzed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    make = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    variant = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    model_year = table.Column<int>(type: "integer", nullable: true),
                    vin = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    price_sek = table.Column<decimal>(type: "numeric", nullable: true),
                    odometer_kilometres = table.Column<decimal>(type: "numeric", nullable: true),
                    seller_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    locality = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    county = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    published_date = table.Column<DateOnly>(type: "date", nullable: true),
                    advertised_updated_date = table.Column<DateOnly>(type: "date", nullable: true),
                    image_count = table.Column<int>(type: "integer", nullable: true),
                    fuel_types = table.Column<string[]>(type: "text[]", nullable: true),
                    transmission = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    drivetrain = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    body_type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    colour = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    horsepower = table.Column<int>(type: "integer", nullable: true),
                    engine_displacement_cubic_centimetres = table.Column<decimal>(type: "numeric", nullable: true),
                    energy_consumptions = table.Column<string>(type: "jsonb", nullable: true),
                    annual_vehicle_tax_sek = table.Column<decimal>(type: "numeric", nullable: true),
                    owner_count = table.Column<int>(type: "integer", nullable: true),
                    first_registration_date = table.Column<DateOnly>(type: "date", nullable: true),
                    last_inspection_date = table.Column<DateOnly>(type: "date", nullable: true),
                    next_inspection_date = table.Column<DateOnly>(type: "date", nullable: true),
                    tow_bar = table.Column<bool>(type: "boolean", nullable: true),
                    equipment_known = table.Column<bool>(type: "boolean", nullable: false),
                    seller_claims = table.Column<string>(type: "jsonb", nullable: true),
                    condition_notes = table.Column<string>(type: "jsonb", nullable: true),
                    field_provenance = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vehicle_listings", x => x.id);
                    table.CheckConstraint("ck_vehicle_listings_annual_tax", "annual_vehicle_tax_sek IS NULL OR annual_vehicle_tax_sek BETWEEN 0 AND 100000000");
                    table.CheckConstraint("ck_vehicle_listings_body_type", "body_type IS NULL OR body_type IN ('Sedan', 'Hatchback', 'Wagon', 'Suv', 'Coupe', 'Convertible', 'Minivan', 'Pickup', 'Van', 'Other')");
                    table.CheckConstraint("ck_vehicle_listings_condition_notes", "condition_notes IS NULL OR (jsonb_typeof(condition_notes) = 'array' AND jsonb_array_length(condition_notes) <= 10)");
                    table.CheckConstraint("ck_vehicle_listings_drivetrain", "drivetrain IS NULL OR drivetrain IN ('FrontWheelDrive', 'RearWheelDrive', 'AllWheelDrive')");
                    table.CheckConstraint("ck_vehicle_listings_energy_consumptions", "energy_consumptions IS NULL OR (jsonb_typeof(energy_consumptions) = 'array' AND jsonb_array_length(energy_consumptions) <= 2)");
                    table.CheckConstraint("ck_vehicle_listings_engine_displacement", "engine_displacement_cubic_centimetres IS NULL OR (engine_displacement_cubic_centimetres > 0 AND engine_displacement_cubic_centimetres <= 100000)");
                    table.CheckConstraint("ck_vehicle_listings_extraction_metadata", "(requested_model IS NULL AND prompt_version IS NULL AND extraction_schema_version IS NULL) OR (requested_model IS NOT NULL AND length(btrim(requested_model)) BETWEEN 1 AND 100 AND prompt_version IS NOT NULL AND prompt_version = 2 AND extraction_schema_version IS NOT NULL AND extraction_schema_version = 2)");
                    table.CheckConstraint("ck_vehicle_listings_field_provenance", "jsonb_typeof(field_provenance) = 'object'");
                    table.CheckConstraint("ck_vehicle_listings_fuel_types", "fuel_types IS NULL OR (cardinality(fuel_types) <= 9 AND fuel_types <@ ARRAY['Petrol','Diesel','Electricity','Ethanol','Biogas','NaturalGas','LiquefiedPetroleumGas','Hydrogen','Other']::text[])");
                    table.CheckConstraint("ck_vehicle_listings_horsepower", "horsepower IS NULL OR horsepower BETWEEN 1 AND 10000");
                    table.CheckConstraint("ck_vehicle_listings_image_count", "image_count IS NULL OR image_count BETWEEN 0 AND 10000");
                    table.CheckConstraint("ck_vehicle_listings_missing_fields", "cardinality(missing_fields) <= 31 AND missing_fields <@ ARRAY['RegistrationNumber','Make','Model','Variant','ModelYear','Vin','PriceSek','OdometerKilometres','SellerType','Locality','County','PublishedDate','UpdatedDate','ImageCount','FuelTypes','Transmission','Drivetrain','BodyType','Colour','Horsepower','EngineDisplacementCubicCentimetres','EnergyConsumptions','AnnualVehicleTaxSek','OwnerCount','FirstRegistrationDate','LastInspectionDate','NextInspectionDate','TowBar','Equipment','SellerClaims','ConditionNotes']::text[]");
                    table.CheckConstraint("ck_vehicle_listings_model_year", "model_year IS NULL OR model_year BETWEEN 1886 AND 2100");
                    table.CheckConstraint("ck_vehicle_listings_odometer", "odometer_kilometres IS NULL OR odometer_kilometres BETWEEN 0 AND 10000000");
                    table.CheckConstraint("ck_vehicle_listings_owner_count", "owner_count IS NULL OR owner_count BETWEEN 0 AND 10000");
                    table.CheckConstraint("ck_vehicle_listings_price", "price_sek IS NULL OR price_sek BETWEEN 0 AND 100000000");
                    table.CheckConstraint("ck_vehicle_listings_seller_claims", "seller_claims IS NULL OR (jsonb_typeof(seller_claims) = 'array' AND jsonb_array_length(seller_claims) <= 20)");
                    table.CheckConstraint("ck_vehicle_listings_seller_type", "seller_type IS NULL OR seller_type IN ('Private', 'Dealer')");
                    table.CheckConstraint("ck_vehicle_listings_status", "status IN ('Complete', 'Partial', 'Unavailable')");
                    table.CheckConstraint("ck_vehicle_listings_transmission", "transmission IS NULL OR transmission IN ('Manual', 'Automatic')");
                    table.CheckConstraint("ck_vehicle_listings_urls", "length(submitted_url) BETWEEN 1 AND 2048 AND length(normalized_url) BETWEEN 1 AND 2048");
                    table.CheckConstraint("ck_vehicle_listings_versions", "listing_version >= 1 AND listing_schema_version = 1");
                    table.ForeignKey(
                        name: "fk_vehicle_listings_vehicles",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "listing_equipment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    listing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    value = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_listing_equipment", x => x.id);
                    table.CheckConstraint("ck_listing_equipment_position", "position BETWEEN 0 AND 99");
                    table.CheckConstraint("ck_listing_equipment_value", "length(btrim(value)) BETWEEN 1 AND 100");
                    table.ForeignKey(
                        name: "fk_listing_equipment_vehicle_listings",
                        column: x => x.listing_id,
                        principalTable: "vehicle_listings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "listing_sources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    listing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    matches_submitted_url = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_listing_sources", x => x.id);
                    table.CheckConstraint("ck_listing_sources_position", "position >= 0");
                    table.CheckConstraint("ck_listing_sources_url", "length(btrim(url)) BETWEEN 1 AND 2048");
                    table.ForeignKey(
                        name: "fk_listing_sources_vehicle_listings",
                        column: x => x.listing_id,
                        principalTable: "vehicle_listings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_listing_equipment_listing_id_position",
                table: "listing_equipment",
                columns: new[] { "listing_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_listing_sources_listing_id_position",
                table: "listing_sources",
                columns: new[] { "listing_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_vehicle_listings_vehicle_id",
                table: "vehicle_listings",
                column: "vehicle_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM vehicles AS vehicle
                WHERE vehicle.id IN (
                    SELECT listing.vehicle_id
                    FROM vehicle_listings AS listing
                    LEFT JOIN saved_cost_scenarios AS scenario
                        ON scenario.vehicle_id = listing.vehicle_id
                    WHERE scenario.vehicle_id IS NULL
                )
                """);

            migrationBuilder.DropTable(
                name: "listing_equipment");

            migrationBuilder.DropTable(
                name: "listing_sources");

            migrationBuilder.DropTable(
                name: "vehicle_listings");
        }
    }
}
