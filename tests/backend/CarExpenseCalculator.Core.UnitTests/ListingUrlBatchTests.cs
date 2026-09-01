using CarExpenseCalculator.Core.Listings;
using Xunit;

namespace CarExpenseCalculator.Core.UnitTests;

public sealed class ListingUrlBatchTests
{
    [Theory]
    [InlineData("https://example.com/item/123", "https://example.com/item/123?ci=2")]
    [InlineData("https://example.com/item/123", "https://example.com/item/123/")]
    [InlineData("http://example.com/item/123", "https://example.com/item/123")]
    public void Create_rejects_page_identity_duplicates(string first, string duplicate)
    {
        var exception = Assert.Throws<ListingValidationException>(
            () => ListingUrlBatch.Create([first, duplicate]));

        var error = Assert.Single(exception.Errors);
        Assert.Equal("urls[1]", error.Path);
        Assert.Contains("urls[0]", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_preserves_order_and_defensively_copies_input()
    {
        var input = new[]
        {
            "https://example.com/first",
            "https://example.com/second",
        };

        var batch = ListingUrlBatch.Create(input);
        input[0] = "https://example.com/changed";

        Assert.Equal(
            ["https://example.com/first", "https://example.com/second"],
            batch.Urls.Select(url => url.Value));
        Assert.IsAssignableFrom<IReadOnlyList<ListingUrl>>(batch.Urls);
    }

    [Fact]
    public void Create_accepts_one_through_ten_unique_urls()
    {
        Assert.Single(ListingUrlBatch.Create(["https://example.com/1"]).Urls);
        Assert.Equal(
            10,
            ListingUrlBatch.Create(Enumerable.Range(1, 10).Select(index => $"https://example.com/{index}")).Urls.Count);
    }

    [Fact]
    public void Create_accumulates_count_invalid_and_duplicate_errors()
    {
        var values = new[]
        {
            "https://example.com/one",
            "invalid",
            "https://example.com/one?tracking=1",
            "https://example.com/four",
            "https://example.com/five",
            "https://example.com/six",
            "https://example.com/seven",
            "https://example.com/eight",
            "https://example.com/nine",
            "https://example.com/ten",
            "https://example.com/eleven",
        };

        var exception = Assert.Throws<ListingValidationException>(() => ListingUrlBatch.Create(values));

        Assert.Equal(["urls", "urls[1]", "urls[2]"], exception.Errors.Select(error => error.Path));
    }

    [Fact]
    public void Create_rejects_empty_and_null_inputs()
    {
        var exception = Assert.Throws<ListingValidationException>(() => ListingUrlBatch.Create([]));

        Assert.Equal("urls", Assert.Single(exception.Errors).Path);
        Assert.Throws<ArgumentNullException>(() => ListingUrlBatch.Create(null!));
    }
}
