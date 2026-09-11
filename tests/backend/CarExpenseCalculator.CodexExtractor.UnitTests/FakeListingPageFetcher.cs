using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Extraction.Contracts;

namespace CarExpenseCalculator.CodexExtractor.UnitTests;

internal sealed class FakeListingPageFetcher : IListingPageFetcher
{
    public Task<RetrievedPage> FetchAsync(ListingUrl url, CancellationToken cancellationToken) =>
        Task.FromResult(new RetrievedPage(url, "<main><h1>Testbil</h1><div><h2>Annonsinformation</h2><p>Annons-ID</p><p>1</p></div></main>"));

    public static RetrievedListingContent Content(string url = "https://www.blocket.se/mobility/item/1") =>
        new(url, "Testbil", null, "1", null, null, null, null, null, null, null);
}
