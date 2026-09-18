using CarExpenseCalculator.Infrastructure.Persistence.SavedListings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarExpenseCalculator.Infrastructure.Persistence.ListingReviewDrafts;

public sealed record ListingReviewDraft(Guid Id, long Revision, int SchemaVersion, string ListingReference,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, SavedListingInput Input);

public interface IListingReviewDraftStore
{
    Task<IReadOnlyList<ListingReviewDraft>> ListAsync(CancellationToken ct = default);
    Task<ListingReviewDraft?> GetAsync(Guid id, CancellationToken ct = default);
    Task<ListingReviewDraft> CreateAsync(SavedListingInput input, CancellationToken ct = default);
    Task<ListingReviewDraft> ReplaceAsync(Guid id, long expectedRevision, SavedListingInput input, CancellationToken ct = default);
    Task DeleteAsync(Guid id, long expectedRevision, CancellationToken ct = default);
    Task<SavedListing> AdoptAsync(Guid id, long expectedRevision, Guid? existingVehicleId = null,
        long? expectedVehicleRevision = null, CancellationToken ct = default);
}

internal sealed class ListingReviewDraftEntity
{
    public Guid Id { get; set; }
    public long Revision { get; set; }
    public int SchemaVersion { get; set; } = 1;
    public required string ListingReference { get; set; }
    public required string InputJson { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class ListingReviewDraftConfiguration : IEntityTypeConfiguration<ListingReviewDraftEntity>
{
    public void Configure(EntityTypeBuilder<ListingReviewDraftEntity> builder)
    {
        builder.ToTable("listing_review_drafts", table =>
        {
            table.HasCheckConstraint("ck_listing_review_drafts_version", "revision >= 1 AND schema_version = 1");
            table.HasCheckConstraint("ck_listing_review_drafts_payload", "jsonb_typeof(input) = 'object'");
            table.HasCheckConstraint("ck_listing_review_drafts_dates", "updated_at_utc >= created_at_utc");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
        builder.Property(x => x.SchemaVersion).HasColumnName("schema_version");
        builder.Property(x => x.ListingReference).HasColumnName("listing_reference").HasMaxLength(2048);
        builder.Property(x => x.InputJson).HasColumnName("input").HasColumnType("jsonb");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.HasIndex(x => x.ListingReference).IsUnique();
    }
}
