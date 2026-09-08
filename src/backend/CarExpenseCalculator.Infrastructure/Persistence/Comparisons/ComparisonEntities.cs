using CarExpenseCalculator.Infrastructure.Persistence.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarExpenseCalculator.Infrastructure.Persistence.Comparisons;

internal sealed class RuleProfileEntity
{
    public int Id { get; set; } = 1;
    public long Revision { get; set; }
    public int SchemaVersion { get; set; } = 1;
    public string? InputJson { get; set; }
}
internal sealed class VehicleComparisonFactsEntity
{
    public Guid VehicleId { get; set; }
    public required VehicleEntity Vehicle { get; set; }
    public int SchemaVersion { get; set; } = 1;
    public required string InputJson { get; set; }
    public long? ReviewedListingVersion { get; set; }
    public DateTimeOffset? CostConfirmedAt { get; set; }
}
internal sealed class RuleProfileConfiguration : IEntityTypeConfiguration<RuleProfileEntity>
{
    public void Configure(EntityTypeBuilder<RuleProfileEntity> builder)
    {
        builder.ToTable("rule_profile", t =>
        {
            t.HasCheckConstraint("ck_rule_profile_singleton", "id = 1");
            t.HasCheckConstraint("ck_rule_profile_version", "schema_version >= 1 AND revision >= 0");
            t.HasCheckConstraint("ck_rule_profile_payload", "(input IS NULL AND revision = 0) OR (input IS NOT NULL AND jsonb_typeof(input) = 'object' AND revision > 0)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
        builder.Property(x => x.SchemaVersion).HasColumnName("schema_version");
        builder.Property(x => x.InputJson).HasColumnName("input").HasColumnType("jsonb");
        builder.HasData(new RuleProfileEntity());
    }
}
internal sealed class VehicleComparisonFactsConfiguration : IEntityTypeConfiguration<VehicleComparisonFactsEntity>
{
    public void Configure(EntityTypeBuilder<VehicleComparisonFactsEntity> builder)
    {
        builder.ToTable("vehicle_comparison_facts", t =>
        {
            t.HasCheckConstraint("ck_vehicle_comparison_facts_payload", "jsonb_typeof(input) = 'object'");
            t.HasCheckConstraint("ck_vehicle_comparison_facts_version", "schema_version >= 1 AND (reviewed_listing_version IS NULL OR reviewed_listing_version >= 1)");
        });
        builder.HasKey(x => x.VehicleId);
        builder.Property(x => x.VehicleId).HasColumnName("vehicle_id").ValueGeneratedNever();
        builder.Property(x => x.SchemaVersion).HasColumnName("schema_version");
        builder.Property(x => x.InputJson).HasColumnName("input").HasColumnType("jsonb");
        builder.Property(x => x.ReviewedListingVersion).HasColumnName("reviewed_listing_version");
        builder.Property(x => x.CostConfirmedAt).HasColumnName("cost_confirmed_at");
        builder.HasOne(x => x.Vehicle).WithOne(x => x.ComparisonFacts)
            .HasForeignKey<VehicleComparisonFactsEntity>(x => x.VehicleId).OnDelete(DeleteBehavior.Cascade);
    }
}
