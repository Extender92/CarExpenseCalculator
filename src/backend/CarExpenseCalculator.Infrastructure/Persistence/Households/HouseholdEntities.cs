using CarExpenseCalculator.Infrastructure.Persistence.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarExpenseCalculator.Infrastructure.Persistence.Households;

internal sealed class HouseholdStateEntity
{
    public int Id { get; set; } = 1;
    public long ProfileRevision { get; set; }
    public long TransitionRevision { get; set; }
    public int SchemaVersion { get; set; } = 1;
    public string? ProfileJson { get; set; }
}

internal sealed class VehicleCostInputEntity
{
    public Guid VehicleId { get; set; }
    public required VehicleEntity Vehicle { get; set; }
    public int SchemaVersion { get; set; } = 1;
    public required string InputJson { get; set; }
    public long? SourceListingVersion { get; set; }
}

internal sealed class VehicleDraftEntity
{
    public int Id { get; set; } = 1;
    public long Revision { get; set; }
    public int SchemaVersion { get; set; } = 1;
    public string? RegistrationNumber { get; set; }
    public Guid? BaseVehicleId { get; set; }
    public long? BaseVehicleRevision { get; set; }
    public string? InputJson { get; set; }

    public void Clear()
    {
        Revision = checked(Revision + 1);
        RegistrationNumber = null;
        BaseVehicleId = null;
        BaseVehicleRevision = null;
        InputJson = null;
    }
}

internal sealed class HouseholdStateConfiguration : IEntityTypeConfiguration<HouseholdStateEntity>
{
    public void Configure(EntityTypeBuilder<HouseholdStateEntity> builder)
    {
        builder.ToTable("household_state", table =>
        {
            table.HasCheckConstraint("ck_household_state_singleton", "id = 1");
            table.HasCheckConstraint("ck_household_state_revisions", "profile_revision >= 0 AND transition_revision >= 0 AND schema_version >= 1");
            table.HasCheckConstraint("ck_household_state_profile", "(profile IS NULL AND profile_revision = 0) OR (profile IS NOT NULL AND jsonb_typeof(profile) = 'object' AND profile_revision > 0)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.ProfileRevision).HasColumnName("profile_revision").IsConcurrencyToken();
        builder.Property(x => x.TransitionRevision).HasColumnName("transition_revision").IsConcurrencyToken();
        builder.Property(x => x.SchemaVersion).HasColumnName("schema_version");
        builder.Property(x => x.ProfileJson).HasColumnName("profile").HasColumnType("jsonb");
        builder.HasData(new HouseholdStateEntity());
    }
}

internal sealed class VehicleCostInputConfiguration : IEntityTypeConfiguration<VehicleCostInputEntity>
{
    public void Configure(EntityTypeBuilder<VehicleCostInputEntity> builder)
    {
        builder.ToTable("vehicle_cost_inputs", table =>
        {
            table.HasCheckConstraint("ck_vehicle_cost_inputs_version", "schema_version >= 1 AND (source_listing_version IS NULL OR source_listing_version >= 1)");
            table.HasCheckConstraint("ck_vehicle_cost_inputs_payload", "jsonb_typeof(input) = 'object'");
        });
        builder.HasKey(x => x.VehicleId);
        builder.Property(x => x.VehicleId).HasColumnName("vehicle_id").ValueGeneratedNever();
        builder.Property(x => x.SchemaVersion).HasColumnName("schema_version");
        builder.Property(x => x.InputJson).HasColumnName("input").HasColumnType("jsonb");
        builder.Property(x => x.SourceListingVersion).HasColumnName("source_listing_version");
        builder.HasOne(x => x.Vehicle).WithOne(x => x.HouseholdCostInput)
            .HasForeignKey<VehicleCostInputEntity>(x => x.VehicleId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class VehicleDraftConfiguration : IEntityTypeConfiguration<VehicleDraftEntity>
{
    public void Configure(EntityTypeBuilder<VehicleDraftEntity> builder)
    {
        builder.ToTable("vehicle_draft", table =>
        {
            table.HasCheckConstraint("ck_vehicle_draft_singleton", "id = 1");
            table.HasCheckConstraint("ck_vehicle_draft_revision", "revision >= 0 AND schema_version >= 1");
            table.HasCheckConstraint("ck_vehicle_draft_payload", "(input IS NULL AND registration_number IS NULL AND base_vehicle_id IS NULL AND base_vehicle_revision IS NULL) OR (input IS NOT NULL AND jsonb_typeof(input) = 'object' AND registration_number IS NOT NULL)");
            table.HasCheckConstraint("ck_vehicle_draft_base", "(base_vehicle_id IS NULL AND base_vehicle_revision IS NULL) OR (base_vehicle_id IS NOT NULL AND base_vehicle_revision IS NOT NULL AND base_vehicle_revision >= 1)");
            table.HasCheckConstraint("ck_vehicle_draft_registration", "registration_number IS NULL OR registration_number ~ '^[A-HJ-PR-UW-Z]{3}[0-9]{2}([0-9]|[A-HJ-NPR-UW-Z])$'");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
        builder.Property(x => x.SchemaVersion).HasColumnName("schema_version");
        builder.Property(x => x.RegistrationNumber).HasColumnName("registration_number").HasMaxLength(6);
        builder.Property(x => x.BaseVehicleId).HasColumnName("base_vehicle_id");
        builder.Property(x => x.BaseVehicleRevision).HasColumnName("base_vehicle_revision");
        builder.Property(x => x.InputJson).HasColumnName("input").HasColumnType("jsonb");
        builder.HasOne<VehicleEntity>().WithMany().HasForeignKey(x => x.BaseVehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasData(new VehicleDraftEntity());
    }
}
