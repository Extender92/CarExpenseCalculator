namespace CarExpenseCalculator.Extraction.Contracts;

/// <summary>Application-owned page content. Never populated from model output.</summary>
public sealed record RetrievedListingContent
{
    public RetrievedListingContent(string sourceUrl, string title, string? subtitle, string listingId,
        string? price, string? description, IReadOnlyList<ExtractedListingSpecification>? specifications,
        IReadOnlyList<string>? equipment, IReadOnlyList<ExtractedSellerAnswer>? sellerAnswers,
        string? location, string? updated)
    {
        SourceUrl = sourceUrl;
        Title = title;
        Subtitle = subtitle;
        ListingId = listingId;
        Price = price;
        Description = description;
        Specifications = specifications is null ? null : Array.AsReadOnly(specifications.ToArray());
        Equipment = equipment is null ? null : Array.AsReadOnly(equipment.ToArray());
        SellerAnswers = sellerAnswers is null ? null : Array.AsReadOnly(sellerAnswers.ToArray());
        Location = location;
        Updated = updated;
    }

    public string SourceUrl { get; }
    public string Title { get; }
    public string? Subtitle { get; }
    public string ListingId { get; }
    public string? Price { get; }
    public string? Description { get; }
    public IReadOnlyList<ExtractedListingSpecification>? Specifications { get; }
    public IReadOnlyList<string>? Equipment { get; }
    public IReadOnlyList<ExtractedSellerAnswer>? SellerAnswers { get; }
    public string? Location { get; }
    public string? Updated { get; }
}
