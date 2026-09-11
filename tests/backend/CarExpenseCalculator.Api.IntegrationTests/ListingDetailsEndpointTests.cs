using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CarExpenseCalculator.Api.Contracts.SavedListings;
using Xunit;
using static CarExpenseCalculator.Api.IntegrationTests.SavedListingTestData;
using static CarExpenseCalculator.Api.IntegrationTests.HouseholdApiTestData;

namespace CarExpenseCalculator.Api.IntegrationTests;

public sealed class ListingDetailsEndpointTests(SavedListingApiFactory factory) : IClassFixture<SavedListingApiFactory>, IAsyncLifetime
{
    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    private static ReviewedListingInput Input(int length = 32000)
    {
        var input = Complete("TST123");
        var source = input.Draft.PriceSek!.Provenance;
        return input with { PromptVersion = 3, SchemaVersion = 3, Sources = [], Draft = input.Draft with
        {
            Details = new()
            {
                Description = Value(new string('å', length), source),
                Seats = Value(5, source), Doors = Value(0, source),
                WeightKilograms = Value(1370.123456789012345678901234m, source),
                WeightLabel = Value("Vikt", source), WeightCategory = Value("unspecified", source),
                UpdatedLocalDateTime = Value("2026-09-08T16:35:00", source), Specifications = [],
                SellerAnswers = [Value(new SellerAnswerInput("Har bilen några skulder?", "Nej"), source)],
            },
        }};
    }

    [Fact]
    public async Task Full_multibyte_listing_survives_save_read_and_complete_comparison_without_page_metadata()
    {
        using var client = factory.CreateClient();
        using var created = await client.PostAsJsonAsync("/api/saved-listings", new CreateSavedListingRequest { RegistrationNumber = "TST123", Listing = Input() });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var saved = await Json(created, HttpStatusCode.Created);
        Assert.False(saved["sourcePageObserved"]!.GetValue<bool>());
        Assert.Equal(2, saved["listingSchemaVersion"]!.GetValue<int>());
        var id = saved["vehicleId"]!.GetValue<string>();
        var loaded = await Json(await client.GetAsync($"/api/saved-listings/{id}"));
        Assert.True(JsonNode.DeepEquals(saved["listing"], loaded["listing"]));
        var baseline = await Json(await client.GetAsync("/api/comparisons/baseline"));
        var result = await Json(await client.PostAsJsonAsync(CompleteComparisonApiData.Route, CompleteComparisonApiData.Stored(baseline)));
        Assert.Equal(2, result["transportVersion"]!.GetValue<int>());
        var listing = Assert.Single(result["listings"]!.AsArray())!;
        Assert.True(JsonNode.DeepEquals(saved["listing"], listing["listing"]));
        var details = listing["listing"]!["details"]!;
        Assert.Equal(32000, details["description"]!["value"]!.GetValue<string>().Length);
        Assert.Equal(1370.123456789012345678901234m, details["weightKilograms"]!["value"]!.GetValue<decimal>());
        Assert.Equal("unverified", details["seats"]!["provenance"]!["verification"]!.GetValue<string>());
        Assert.Equal("Nej", details["sellerAnswers"]![0]!["value"]!["answer"]!.GetValue<string>());
        Assert.Empty(details["specifications"]!.AsArray());
        Assert.Null(details["updatedTimeZone"]);
        foreach (var view in CompleteComparisonApiData.Views(result)) Assert.Single(view["candidates"]!.AsArray());
        var manual = await Json(await client.PostAsJsonAsync(CompleteComparisonApiData.Route, CompleteComparisonApiData.Manual()));
        Assert.Empty(manual["listings"]!.AsArray());
        Assert.Equal(0, factory.ExtractionService.ExtractionCallCount);
    }

    [Fact]
    public async Task Oversized_description_is_a_field_error_and_creates_no_vehicle()
    {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/saved-listings", new CreateSavedListingRequest { RegistrationNumber = "TST123", Listing = Input(32001) });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Contains("description", body["errors"]!.ToJsonString());
        Assert.Empty((await Json(await client.GetAsync("/api/saved-listings"))).AsArray());
    }
}
