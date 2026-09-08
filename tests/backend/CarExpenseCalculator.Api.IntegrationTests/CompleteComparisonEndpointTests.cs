using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using CarExpenseCalculator.Infrastructure.Persistence.Comparisons;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using static CarExpenseCalculator.Api.IntegrationTests.HouseholdApiTestData;

namespace CarExpenseCalculator.Api.IntegrationTests;

internal static class CompleteComparisonApiData
{
    public const string Route = "/api/comparisons/preview-all";
    public static JsonObject Manual(int count = 1) => ComparisonApiTestData.Preview(Enumerable.Range(100, count)
        .Select(i => ComparisonApiTestData.Candidate($"ABC{i:000}")).ToArray());
    public static IEnumerable<JsonNode> Views(JsonNode result) => new[] { "baseline", "favorable", "cautious" }.Select(x => result["views"]![x]!);
    public static JsonObject Stored(JsonNode baseline) => new()
    {
        ["mode"] = "stored", ["requestId"] = "all-stored", ["profile"] = Profile(), ["rules"] = ComparisonApiTestData.Rules(),
        ["asOfDate"] = "2026-09-08", ["storedBase"] = new JsonObject
        {
            ["baselineToken"] = baseline["baselineToken"]!.DeepClone(),
            ["householdProfileRevision"] = baseline["householdProfileRevision"]!.DeepClone(),
            ["ruleProfileRevision"] = baseline["ruleProfileRevision"]!.DeepClone(),
        },
    };
    public static JsonObject Override(JsonNode vehicle) => new()
    {
        ["vehicleId"] = vehicle["vehicleId"]!.DeepClone(), ["registrationNumber"] = vehicle["registrationNumber"]!.DeepClone(),
        ["storedBase"] = new JsonObject { ["vehicleRevision"] = vehicle["revision"]!.DeepClone(),
            ["listing"] = new JsonObject { ["version"] = vehicle["currentListingVersion"]?.DeepClone() } },
    };
}

public sealed class CompleteComparisonEndpointTests
{
    [Fact]
    public async Task All_sensitivity_values_and_confirmation_use_the_same_effective_inputs()
    {
        using var factory = new ManualCalculationApiFactory(); using var client = factory.CreateClient();
        var request = CompleteComparisonApiData.Manual();
        request["rules"] = JsonNode.Parse("""{"preferences":[{"criterionKey":"netCostSek","weight":1,"minimumEvidence":"userConfirmed","zeroPoint":20000,"fullPoint":0}]}""");
        var car = request["candidates"]![0]!;
        car["costInput"] = Vehicle(); car["facts"]!["costConfirmation"] = "confirm";
        car["costInput"]!["service"]!["items"] = JsonNode.Parse("""[{"key":"service","label":"Service","cadence":"annual","dueMonthOfYear":3,"amountSek":{"favorable":100,"baseline":200,"cautious":300}}]""");
        var result = await Json(await client.PostAsJsonAsync(CompleteComparisonApiData.Route, request));
        var expected = new[] { 10200m, 10100m, 10300m };
        var i = 0;
        foreach (var view in CompleteComparisonApiData.Views(result))
        {
            var actual = view["candidates"]![0]!;
            Assert.Equal(expected[i], actual["cost"]!["totals"]!["ownershipCost"]!["completeTotalSek"]!.GetValue<decimal>());
            Assert.Equal((20000 - expected[i++]) / 200, actual["score"]!["lower"]!.GetValue<decimal>());
        }
        car["costInput"]!["service"]!["items"]![0]!["amountSek"] = null;
        result = await Json(await client.PostAsJsonAsync(CompleteComparisonApiData.Route, request));
        Assert.Null(result["views"]!["favorable"]!["candidates"]![0]!["cost"]!["totals"]!["ownershipCost"]!["completeTotalSek"]);
        Assert.Null(result["views"]!["baseline"]!["candidates"]![0]!["cost"]!["totals"]!["ownershipCost"]!["completeTotalSek"]);
    }

    [Fact]
    public async Task Raw_cost_and_score_order_crosses_the_HTTP_group_boundary()
    {
        using var factory = new ManualCalculationApiFactory(); using var client = factory.CreateClient();
        var request = CompleteComparisonApiData.Manual(101);
        request["rules"] = JsonNode.Parse("""{"preferences":[{"criterionKey":"netCostSek","weight":1,"minimumEvidence":"advertised","zeroPoint":200,"fullPoint":0}]}""");
        for (var i = 0; i < 101; i++) request["candidates"]![i]!["costInput"] = Vehicle();
        request["candidates"]![0]!["costInput"]!["residual"]!["value"]!["single"] = 49899.998m;
        request["candidates"]![100]!["costInput"]!["residual"]!["value"]!["single"] = 49899.999m;
        var result = await Json(await client.PostAsJsonAsync(CompleteComparisonApiData.Route, request));
        foreach (var view in CompleteComparisonApiData.Views(result))
        {
            var cars = view["candidates"]!;
            Assert.Equal(cars[0]!["score"]!.ToJsonString(), cars[100]!["score"]!.ToJsonString());
            Assert.Equal(100m, cars[100]!["cost"]!["totals"]!["ownershipCost"]!["completeTotalSek"]!.GetValue<decimal>());
            Assert.Equal(cars[100]!["vehicleId"]!.GetValue<Guid>(), view["costOrder"]![0]!.GetValue<Guid>());
            Assert.Equal(cars[100]!["vehicleId"]!.GetValue<Guid>(), view["scoreOrder"]![0]!.GetValue<Guid>());
            Assert.True(cars[100]!["isDefinitePreferenceWinner"]!.GetValue<bool>());
        }
    }

    [Fact]
    public async Task A_genuinely_large_manual_input_needs_no_storage_or_upload_session()
    {
        using var factory = new ManualCalculationApiFactory(); using var client = factory.CreateClient();
        var request = CompleteComparisonApiData.Manual(101);
        foreach (var car in request["candidates"]!.AsArray())
        {
            var cost = Vehicle();
            cost["customCosts"]!["items"] = new JsonArray(Enumerable.Range(0, 25).Select(i => (JsonNode)new JsonObject
            {
                ["key"] = $"estimate-{i}", ["label"] = "Written estimate", ["evidenceNote"] = new string('x', 1000),
                ["cadence"] = "once", ["monthOffset"] = 1, ["amountSek"] = new JsonObject { ["single"] = 1 },
            }).ToArray());
            car!["costInput"] = cost;
        }
        Assert.True(Encoding.UTF8.GetByteCount(request.ToJsonString()) > 2 * 1024 * 1024);
        var result = await Json(await client.PostAsJsonAsync(CompleteComparisonApiData.Route, request));
        Assert.Equal(101, result["candidateCount"]!.GetValue<int>());
        Assert.All(CompleteComparisonApiData.Views(result), view => Assert.Equal(101, view["candidates"]!.AsArray().Count));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(101)]
    [InlineData(250)]
    public async Task Manual_complete_comparison_uses_no_storage_and_keeps_one_generation(int count)
    {
        using var factory = new ManualCalculationApiFactory();
        using var isolated = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IComparisonSnapshotStore>();
            s.AddScoped<IComparisonSnapshotStore>(_ => throw new InvalidOperationException("No storage may be resolved."));
        }));
        using var client = isolated.CreateClient();
        var result = await Json(await client.PostAsJsonAsync(CompleteComparisonApiData.Route, CompleteComparisonApiData.Manual(count)));
        Assert.Equal(count, result["candidateCount"]!.GetValue<int>());
        Assert.Equal(1, result["transportVersion"]!.GetValue<int>());
        Assert.NotEqual(Guid.Empty, result["generationId"]!.GetValue<Guid>());
        Assert.Null(result["baselineToken"]);
        var views = CompleteComparisonApiData.Views(result).ToArray();
        foreach (var view in views)
        {
            Assert.False(view["storageChecked"]!.GetValue<bool>());
            Assert.Equal(count, view["candidates"]!.AsArray().Count);
            Assert.Equal(count, view["costOrder"]!.AsArray().Select(x => x!.GetValue<Guid>()).Distinct().Count());
            Assert.All(view["candidates"]!.AsArray(), x => Assert.Equal(85, x!["score"]!["lower"]!.GetValue<decimal>()));
        }
        if (count > 0)
            Assert.Equal(views[0]["candidates"]![0]!["effectiveFacts"]!.ToJsonString(), views[2]["candidates"]![0]!["effectiveFacts"]!.ToJsonString());
    }

    [Theory]
    [InlineData(3, 2, "automatic", 85, 85, 100)]
    [InlineData(3, 2, null, 45, 85, 60)]
    [InlineData(3, 0, null, 75, 75, 100)]
    [InlineData(3, 2, "manual", 45, 45, 100)]
    [InlineData(1, 4, "manual", 15, 15, 100)]
    public async Task B1_to_B4_are_preserved_in_every_view(int price, int gear, string? transmission, int lower, int upper, int coverage)
    {
        using var factory = new ManualCalculationApiFactory(); using var client = factory.CreateClient();
        var request = ComparisonApiTestData.Preview(ComparisonApiTestData.Candidate(gearbox: transmission));
        request["rules"] = ComparisonApiTestData.Rules(price, gear);
        var result = await Json(await client.PostAsJsonAsync(CompleteComparisonApiData.Route, request));
        foreach (var view in CompleteComparisonApiData.Views(result))
        {
            var car = view["candidates"]![0]!;
            Assert.Equal(lower, car["score"]!["lower"]!.GetValue<decimal>());
            Assert.Equal(upper, car["score"]!["upper"]!.GetValue<decimal>());
            Assert.Equal(coverage, car["coveragePercent"]!.GetValue<decimal>());
        }
    }

    [Fact]
    public async Task Numeric_error_after_100_keeps_other_criteria_and_views()
    {
        using var factory = new ManualCalculationApiFactory(); using var client = factory.CreateClient();
        var request = CompleteComparisonApiData.Manual(101);
        request["candidates"]![100]!["facts"]!["edits"]!["seats"] = ComparisonApiTestData.Manual(0);
        request["profile"]!["periodMonths"] = 0;
        var result = await Json(await client.PostAsJsonAsync(CompleteComparisonApiData.Route, request));
        foreach (var view in CompleteComparisonApiData.Views(result))
        {
            Assert.Contains(view["candidates"]![100]!["errors"]!.AsArray(), x => x!["path"]!.GetValue<string>().StartsWith("candidates[100].facts.seats", StringComparison.Ordinal));
            Assert.Equal(85, view["candidates"]![0]!["score"]!["lower"]!.GetValue<decimal>());
            Assert.NotEmpty(view["profileErrors"]!.AsArray());
        }
    }

    [Theory]
    [InlineData("overrides")]
    [InlineData("storedBase")]
    [InlineData("registry")]
    [InlineData("listing")]
    [InlineData("duplicate")]
    public async Task Manual_mode_rejects_storage_claims_and_cross_group_duplicates(string variant)
    {
        using var factory = new ManualCalculationApiFactory(); using var client = factory.CreateClient();
        var request = CompleteComparisonApiData.Manual(101);
        switch (variant)
        {
            case "overrides": request["overrides"] = new JsonArray(); break;
            case "storedBase": request["storedBase"] = new JsonObject(); break;
            case "registry": request["candidates"]![0]!["facts"]!["edits"]!["purchasePriceSek"]!["manual"]!["verification"] = "registryVerified"; break;
            case "listing": request["candidates"]![0]!["facts"]!["edits"]!["purchasePriceSek"] = new JsonObject { ["kind"] = "listing" }; break;
            default: request["candidates"]![100] = request["candidates"]![0]!.DeepClone(); break;
        }
        await Json(await client.PostAsJsonAsync(CompleteComparisonApiData.Route, request), HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Exact_32_MiB_limit_is_enforced_with_or_without_length(bool chunked)
    {
        using var factory = new ManualCalculationApiFactory();
        factory.UseKestrel(0);
        using var client = factory.CreateClient();
        const int limit = 32 * 1024 * 1024;
        var request = CompleteComparisonApiData.Manual(0); request["requestId"] = "räkna-åäö";
        var json = request.ToJsonString(new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        foreach (var extra in new[] { 0, 1 })
        {
            var body = json + new string(' ', limit - Encoding.UTF8.GetByteCount(json) + extra);
            using HttpContent content = chunked ? new ChunkedContent(body) : new StringContent(body, Encoding.UTF8, "application/json");
            content.Headers.ContentType = new("application/json");
            using var message = new HttpRequestMessage(HttpMethod.Post, CompleteComparisonApiData.Route) { Content = content };
            message.Headers.ExpectContinue = true;
            message.Headers.TransferEncodingChunked = chunked;
            var result = await Json(await client.SendAsync(message), extra == 0 ? HttpStatusCode.OK : HttpStatusCode.RequestEntityTooLarge);
            if (extra > 0) Assert.Equal(limit, result["maximumRequestBytes"]!.GetValue<int>());
        }
    }

    [Fact]
    public async Task Configured_byte_limit_applies_only_to_the_new_route()
    {
        using var factory = new ManualCalculationApiFactory();
        using var configured = factory.WithWebHostBuilder(b => b.UseSetting("COMPARISON_MAX_REQUEST_BYTES", "4096"));
        using var client = configured.CreateClient();
        var body = CompleteComparisonApiData.Manual(0).ToJsonString().PadRight(4097);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        var error = await Json(await client.PostAsync(CompleteComparisonApiData.Route, content), HttpStatusCode.RequestEntityTooLarge);
        Assert.Equal(4096, error["maximumRequestBytes"]!.GetValue<int>());
        using var legacy = new StringContent(body, Encoding.UTF8, "application/json");
        await Json(await client.PostAsync("/api/comparisons/preview", legacy));
    }
}
