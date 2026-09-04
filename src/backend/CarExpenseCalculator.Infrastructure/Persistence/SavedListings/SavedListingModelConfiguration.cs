using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarExpenseCalculator.Infrastructure.Persistence.SavedListings;

internal sealed class VehicleListingEntityConfiguration
    : IEntityTypeConfiguration<VehicleListingEntity>
{
    public void Configure(EntityTypeBuilder<VehicleListingEntity> builder)
    {
        builder.ToTable(
            "vehicle_listings",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_vehicle_listings_versions",
                    "listing_version >= 1 AND listing_schema_version = 1");
                table.HasCheckConstraint(
                    "ck_vehicle_listings_urls",
                    "length(submitted_url) BETWEEN 1 AND 2048 AND length(normalized_url) BETWEEN 1 AND 2048");
                table.HasCheckConstraint(
                    "ck_vehicle_listings_status",
                    "status IN ('Complete', 'Partial', 'Unavailable')");
                table.HasCheckConstraint(
                    "ck_vehicle_listings_missing_fields",
                    $"cardinality(missing_fields) <= 31 AND missing_fields <@ ARRAY[{MissingFieldSql}]::text[]");
                table.HasCheckConstraint(
                    "ck_vehicle_listings_extraction_metadata",
                    "(requested_model IS NULL AND prompt_version IS NULL AND extraction_schema_version IS NULL) "
                    + "OR (requested_model IS NOT NULL AND length(btrim(requested_model)) BETWEEN 1 AND 100 "
                    + "AND prompt_version IS NOT NULL AND prompt_version = 2 "
                    + "AND extraction_schema_version IS NOT NULL AND extraction_schema_version = 2)");
                table.HasCheckConstraint(
                    "ck_vehicle_listings_model_year",
                    "model_year IS NULL OR model_year BETWEEN 1886 AND 2100");
                table.HasCheckConstraint(
                    "ck_vehicle_listings_price",
                    "price_sek IS NULL OR price_sek BETWEEN 0 AND 100000000");
                table.HasCheckConstraint(
                    "ck_vehicle_listings_odometer",
                    "odometer_kilometres IS NULL OR odometer_kilometres BETWEEN 0 AND 10000000");
                table.HasCheckConstraint(
                    "ck_vehicle_listings_seller_type",
                    "seller_type IS NULL OR seller_type IN ('Private', 'Dealer')");
                table.HasCheckConstraint(
                    "ck_vehicle_listings_image_count",
                    "image_count IS NULL OR image_count BETWEEN 0 AND 10000");
                table.HasCheckConstraint(
                    "ck_vehicle_listings_fuel_types",
                    $"fuel_types IS NULL OR (cardinality(fuel_types) <= 9 AND fuel_types <@ ARRAY[{FuelTypeSql}]::text[])");
                table.HasCheckConstraint(
                    "ck_vehicle_listings_transmission",
                    "transmission IS NULL OR transmission IN ('Manual', 'Automatic')");
                table.HasCheckConstraint(
                    "ck_vehicle_listings_drivetrain",
                    "drivetrain IS NULL OR drivetrain IN ('FrontWheelDrive', 'RearWheelDrive', 'AllWheelDrive')");
                table.HasCheckConstraint(
                    "ck_vehicle_listings_body_type",
                    "body_type IS NULL OR body_type IN ('Sedan', 'Hatchback', 'Wagon', 'Suv', 'Coupe', 'Convertible', 'Minivan', 'Pickup', 'Van', 'Other')");
                table.HasCheckConstraint(
                    "ck_vehicle_listings_horsepower",
                    "horsepower IS NULL OR horsepower BETWEEN 1 AND 10000");
                table.HasCheckConstraint(
                    "ck_vehicle_listings_engine_displacement",
                    "engine_displacement_cubic_centimetres IS NULL OR (engine_displacement_cubic_centimetres > 0 AND engine_displacement_cubic_centimetres <= 100000)");
                table.HasCheckConstraint(
                    "ck_vehicle_listings_annual_tax",
                    "annual_vehicle_tax_sek IS NULL OR annual_vehicle_tax_sek BETWEEN 0 AND 100000000");
                table.HasCheckConstraint(
                    "ck_vehicle_listings_owner_count",
                    "owner_count IS NULL OR owner_count BETWEEN 0 AND 10000");
                table.HasCheckConstraint(
                    "ck_vehicle_listings_energy_consumptions",
                    "energy_consumptions IS NULL OR (jsonb_typeof(energy_consumptions) = 'array' AND jsonb_array_length(energy_consumptions) <= 2)");
                table.HasCheckConstraint(
                    "ck_vehicle_listings_seller_claims",
                    "seller_claims IS NULL OR (jsonb_typeof(seller_claims) = 'array' AND jsonb_array_length(seller_claims) <= 20)");
                table.HasCheckConstraint(
                    "ck_vehicle_listings_condition_notes",
                    "condition_notes IS NULL OR (jsonb_typeof(condition_notes) = 'array' AND jsonb_array_length(condition_notes) <= 10)");
                table.HasCheckConstraint(
                    "ck_vehicle_listings_field_provenance",
                    "jsonb_typeof(field_provenance) = 'object'");
            });

        builder.HasKey(entity => entity.Id).HasName("pk_vehicle_listings");
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(entity => entity.VehicleId).HasColumnName("vehicle_id");
        builder.Property(entity => entity.ListingVersion).HasColumnName("listing_version");
        builder.Property(entity => entity.ListingSchemaVersion).HasColumnName("listing_schema_version");
        Text(builder.Property(entity => entity.SubmittedUrl), "submitted_url", 2048, required: true);
        Text(builder.Property(entity => entity.NormalizedUrl), "normalized_url", 2048, required: true);
        builder.Property(entity => entity.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.Property(entity => entity.MissingFields)
            .HasColumnName("missing_fields")
            .HasColumnType("text[]")
            .IsRequired();
        Text(builder.Property(entity => entity.RequestedModel), "requested_model", 100);
        builder.Property(entity => entity.PromptVersion).HasColumnName("prompt_version");
        builder.Property(entity => entity.ExtractionSchemaVersion).HasColumnName("extraction_schema_version");
        Timestamp(builder.Property(entity => entity.AnalyzedAtUtc), "analyzed_at_utc");
        Timestamp(builder.Property(entity => entity.CreatedAtUtc), "created_at_utc");
        Timestamp(builder.Property(entity => entity.UpdatedAtUtc), "updated_at_utc");

        Text(builder.Property(entity => entity.Make), "make", 100);
        Text(builder.Property(entity => entity.Model), "model", 100);
        Text(builder.Property(entity => entity.Variant), "variant", 100);
        builder.Property(entity => entity.ModelYear).HasColumnName("model_year");
        Text(builder.Property(entity => entity.Vin), "vin", 50);
        Numeric(builder.Property(entity => entity.PriceSek), "price_sek");
        Numeric(builder.Property(entity => entity.OdometerKilometres), "odometer_kilometres");
        Enum(builder.Property(entity => entity.SellerType), "seller_type", 16);
        Text(builder.Property(entity => entity.Locality), "locality", 100);
        Text(builder.Property(entity => entity.County), "county", 100);
        builder.Property(entity => entity.PublishedDate).HasColumnName("published_date").HasColumnType("date");
        builder.Property(entity => entity.AdvertisedUpdatedDate).HasColumnName("advertised_updated_date").HasColumnType("date");
        builder.Property(entity => entity.ImageCount).HasColumnName("image_count");
        builder.Property(entity => entity.FuelTypes).HasColumnName("fuel_types").HasColumnType("text[]");
        Enum(builder.Property(entity => entity.Transmission), "transmission", 24);
        Enum(builder.Property(entity => entity.Drivetrain), "drivetrain", 32);
        Enum(builder.Property(entity => entity.BodyType), "body_type", 24);
        Text(builder.Property(entity => entity.Colour), "colour", 100);
        builder.Property(entity => entity.Horsepower).HasColumnName("horsepower");
        Numeric(
            builder.Property(entity => entity.EngineDisplacementCubicCentimetres),
            "engine_displacement_cubic_centimetres");
        Json(builder.Property(entity => entity.EnergyConsumptionsJson), "energy_consumptions");
        Numeric(builder.Property(entity => entity.AnnualVehicleTaxSek), "annual_vehicle_tax_sek");
        builder.Property(entity => entity.OwnerCount).HasColumnName("owner_count");
        builder.Property(entity => entity.FirstRegistrationDate).HasColumnName("first_registration_date").HasColumnType("date");
        builder.Property(entity => entity.LastInspectionDate).HasColumnName("last_inspection_date").HasColumnType("date");
        builder.Property(entity => entity.NextInspectionDate).HasColumnName("next_inspection_date").HasColumnType("date");
        builder.Property(entity => entity.TowBar).HasColumnName("tow_bar");
        builder.Property(entity => entity.EquipmentKnown).HasColumnName("equipment_known");
        Json(builder.Property(entity => entity.SellerClaimsJson), "seller_claims");
        Json(builder.Property(entity => entity.ConditionNotesJson), "condition_notes");
        builder.Property(entity => entity.FieldProvenanceJson)
            .HasColumnName("field_provenance")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.HasIndex(entity => entity.VehicleId)
            .IsUnique()
            .HasDatabaseName("ux_vehicle_listings_vehicle_id");
        builder.HasOne(entity => entity.Vehicle)
            .WithOne(entity => entity.Listing)
            .HasForeignKey<VehicleListingEntity>(entity => entity.VehicleId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_vehicle_listings_vehicles");
    }

    private const string MissingFieldSql =
        "'RegistrationNumber','Make','Model','Variant','ModelYear','Vin','PriceSek','OdometerKilometres','SellerType','Locality','County','PublishedDate','UpdatedDate','ImageCount','FuelTypes','Transmission','Drivetrain','BodyType','Colour','Horsepower','EngineDisplacementCubicCentimetres','EnergyConsumptions','AnnualVehicleTaxSek','OwnerCount','FirstRegistrationDate','LastInspectionDate','NextInspectionDate','TowBar','Equipment','SellerClaims','ConditionNotes'";

    private const string FuelTypeSql =
        "'Petrol','Diesel','Electricity','Ethanol','Biogas','NaturalGas','LiquefiedPetroleumGas','Hydrogen','Other'";

    private static void Text<T>(PropertyBuilder<T> property, string column, int length, bool required = false)
    {
        property.HasColumnName(column).HasMaxLength(length);
        if (required)
        {
            property.IsRequired();
        }
    }

    private static void Numeric<T>(PropertyBuilder<T> property, string column) =>
        property.HasColumnName(column).HasColumnType("numeric");

    private static void Enum<T>(PropertyBuilder<T> property, string column, int length) =>
        property.HasColumnName(column).HasConversion<string>().HasMaxLength(length);

    private static void Timestamp(PropertyBuilder<DateTimeOffset> property, string column) =>
        property.HasColumnName(column).HasColumnType("timestamp with time zone");

    private static void Json(PropertyBuilder<string?> property, string column) =>
        property.HasColumnName(column).HasColumnType("jsonb");
}

internal sealed class ListingSourceEntityConfiguration : IEntityTypeConfiguration<ListingSourceEntity>
{
    public void Configure(EntityTypeBuilder<ListingSourceEntity> builder)
    {
        builder.ToTable(
            "listing_sources",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_listing_sources_position",
                    "position >= 0");
                table.HasCheckConstraint(
                    "ck_listing_sources_url",
                    "length(btrim(url)) BETWEEN 1 AND 2048");
            });
        builder.HasKey(entity => entity.Id).HasName("pk_listing_sources");
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(entity => entity.ListingId).HasColumnName("listing_id");
        builder.Property(entity => entity.Position).HasColumnName("position");
        builder.Property(entity => entity.Url).HasColumnName("url").HasMaxLength(2048).IsRequired();
        builder.Property(entity => entity.MatchesSubmittedUrl).HasColumnName("matches_submitted_url");
        builder.HasIndex(entity => new { entity.ListingId, entity.Position })
            .IsUnique()
            .HasDatabaseName("ux_listing_sources_listing_id_position");
        builder.HasOne(entity => entity.Listing)
            .WithMany(entity => entity.Sources)
            .HasForeignKey(entity => entity.ListingId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_listing_sources_vehicle_listings");
    }
}

internal sealed class ListingEquipmentEntityConfiguration : IEntityTypeConfiguration<ListingEquipmentEntity>
{
    public void Configure(EntityTypeBuilder<ListingEquipmentEntity> builder)
    {
        builder.ToTable(
            "listing_equipment",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_listing_equipment_position",
                    "position BETWEEN 0 AND 99");
                table.HasCheckConstraint(
                    "ck_listing_equipment_value",
                    "length(btrim(value)) BETWEEN 1 AND 100");
            });
        builder.HasKey(entity => entity.Id).HasName("pk_listing_equipment");
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(entity => entity.ListingId).HasColumnName("listing_id");
        builder.Property(entity => entity.Position).HasColumnName("position");
        builder.Property(entity => entity.Value).HasColumnName("value").HasMaxLength(100).IsRequired();
        builder.HasIndex(entity => new { entity.ListingId, entity.Position })
            .IsUnique()
            .HasDatabaseName("ux_listing_equipment_listing_id_position");
        builder.HasOne(entity => entity.Listing)
            .WithMany(entity => entity.Equipment)
            .HasForeignKey(entity => entity.ListingId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_listing_equipment_vehicle_listings");
    }
}
