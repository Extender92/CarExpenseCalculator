using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CarExpenseCalculator.Infrastructure.Persistence.Comparisons;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using static CarExpenseCalculator.Api.IntegrationTests.HouseholdApiTestData;

namespace CarExpenseCalculator.Api.IntegrationTests;

internal static class ComparisonApiTestData
{
    public static JsonObject Manual(object value) => new() { ["kind"] = "manual", ["manual"] = new JsonObject { ["value"] = System.Text.Json.JsonSerializer.SerializeToNode(value) } };
    public static JsonObject Rules(int priceWeight = 3, int gearboxWeight = 2) => new()
    {
        ["preferences"] = new JsonArray(
            new JsonObject { ["criterionKey"] = "purchasePriceSek", ["weight"] = priceWeight, ["minimumEvidence"] = "userConfirmed", ["zeroPoint"] = 100000, ["fullPoint"] = 20000 },
            new JsonObject { ["criterionKey"] = "transmission", ["weight"] = gearboxWeight, ["minimumEvidence"] = "userConfirmed",
                ["preferredValues"] = new JsonArray(new JsonObject { ["transmission"] = "automatic" }) }),
    };
    public static JsonObject Candidate(string registration = "ABC123", decimal price = 40000, string? gearbox = "automatic") => new()
    {
        ["vehicleId"] = Guid.NewGuid(), ["registrationNumber"] = registration,
        ["facts"] = new JsonObject { ["edits"] = new JsonObject { ["purchasePriceSek"] = Manual(price), ["transmission"] = gearbox is null ? null : Manual(gearbox) } },
    };
    public static JsonObject Preview(params JsonObject[] cars) => new()
    {
        ["mode"] = "manual", ["requestId"] = "comparison-1", ["profile"] = Profile(), ["rules"] = Rules(),
        ["asOfDate"] = "2026-09-08", ["candidates"] = new JsonArray(cars.Select(x => (JsonNode)x).ToArray()),
    };
    public static Task<HttpResponseMessage> Post(HttpClient client, JsonObject request) => client.PostAsJsonAsync("/api/comparisons/preview", request);
}

public sealed class ComparisonPreviewEndpointTests
{

    [Theory]
    [InlineData("/api/rule-profile")]
    [InlineData("/api/vehicle-facts/09086400-0000-4000-8000-000000000001")]
    public async Task Unreachable_comparison_storage_returns_503_without_using_extraction(string route)
    {
        using var factory = new ListingAnalysisApiFactory(); using var client = factory.CreateClient();
        var error = await Json(await client.GetAsync(route), HttpStatusCode.ServiceUnavailable);
        Assert.Equal("comparisonStorageUnavailable", error["code"]!.GetValue<string>());
        Assert.DoesNotContain("Password", error.ToJsonString());
        Assert.Equal(0, factory.ExtractionService.ExtractionCallCount);
        Assert.Equal(0, factory.ExtractionService.StatusCallCount);
    }

    [Theory]
    [InlineData(12, null)]
    [InlineData(24, 60000)]
    [InlineData(36, null)]
    public async Task Lease_comparability_and_input_confirmation_are_preserved_through_HTTP(int period, int? expected)
    {
        using var factory = new ManualCalculationApiFactory(); using var client = factory.CreateClient();
        var car = ComparisonApiTestData.Candidate();
        car["costInput"] = Lease(); car["facts"] = new JsonObject { ["costConfirmation"] = "confirm" };
        var input = ComparisonApiTestData.Preview(car); input["profile"]!["periodMonths"] = period;
        input["rules"] = JsonNode.Parse("""
            {"preferences":[{"criterionKey":"netCostSek","weight":1,"minimumEvidence":"userConfirmed","zeroPoint":100000,"fullPoint":0}]}
            """);
        var result = await Json(await ComparisonApiTestData.Post(client, input)); var evaluated = result["candidates"]![0]!;
        Assert.Equal(expected is null ? null : (decimal?)expected.Value, evaluated["cost"]!["totals"]!["ownershipCost"]!["completeTotalSek"]?.GetValue<decimal>());
        Assert.Equal(expected is null ? 0 : 40, evaluated["score"]!["lower"]!.GetValue<decimal>());
        Assert.Equal(expected is null ? 100 : 40, evaluated["score"]!["upper"]!.GetValue<decimal>());
        Assert.Equal("notApplicable", evaluated["effectiveFacts"]!["facts"]!["purchasePriceSek"]!["state"]!.GetValue<string>());
    }

    [Fact]
    public async Task OpenApi_keeps_required_and_nullable_enum_components_separate()
    {
        using var factory = new ManualCalculationApiFactory(); using var client = factory.CreateClient();
        var document = await Json(await client.GetAsync("/api/openapi/v1.json"));
        var schemas = document["components"]!["schemas"]!;
        foreach (var name in new[] { "Transmission", "Drivetrain", "BodyType", "VehicleInputState", "ServiceDocumentationStatus" })
        {
            Assert.DoesNotContain(schemas[name]!["enum"]!.AsArray(), x => x is null);
            Assert.Contains(schemas["Nullable" + name]!["enum"]!.AsArray(), x => x is null);
        }
    }

    [Theory]
    [InlineData("duplicateConflict")]
    [InlineData("nullFuel")]
    [InlineData("invalidText")]
    [InlineData("resolveKnown")]
    [InlineData("duplicateFuel")]
    public async Task Invalid_fact_action_structure_is_400_even_in_manual_preview(string invalid)
    {
        using var factory = new ManualCalculationApiFactory(); using var client = factory.CreateClient();
        var input = ComparisonApiTestData.Preview(ComparisonApiTestData.Candidate());
        var edits = input["candidates"]![0]!["facts"]!["edits"]!;
        if (invalid == "duplicateConflict") edits["seats"] = JsonNode.Parse("""
            {"kind":"conflict","observations":[{"kind":"manual","manual":{"value":5}},{"kind":"manual","manual":{"value":5}}]}
            """);
        if (invalid == "nullFuel") edits["fuelTypes"] = JsonNode.Parse("""{"kind":"manual","manual":{"value":null}}""");
        if (invalid == "duplicateFuel") edits["fuelTypes"] = ComparisonApiTestData.Manual(new[] { "petrol", "petrol" });
        if (invalid == "invalidText") edits["locality"] = ComparisonApiTestData.Manual("");
        if (invalid == "resolveKnown") edits["seats"] = JsonNode.Parse("""{"kind":"resolve","manual":{"value":5}}""");
        await Json(await ComparisonApiTestData.Post(client, input), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task B6_cost_order_and_B7_inclusive_hard_rules_use_the_current_effective_values()
    {
        using var factory = new ManualCalculationApiFactory(); using var client = factory.CreateClient();
        var cars = new[] { ComparisonApiTestData.Candidate("AAA001"), ComparisonApiTestData.Candidate("BBB002"),
            ComparisonApiTestData.Candidate("CCC003"), ComparisonApiTestData.Candidate("DDD004") };
        for (var i = 0; i < cars.Length; i++)
        {
            cars[i]["costInput"] = Vehicle(); cars[i]["costInput"]!["priceSek"] = 40000;
            cars[i]["costInput"]!["residual"]!["value"]!["single"] = i == 0 ? 30000 : 35000;
            cars[i]["facts"]!["costConfirmation"] = "confirm";
            cars[i]["facts"]!["edits"]!["towBar"] = ComparisonApiTestData.Manual(i != 3);
            cars[i]["facts"]!["edits"]!["odometerKilometres"] = ComparisonApiTestData.Manual(200000);
        }
        cars[2]["costInput"]!["tax"] = null;
        var input = ComparisonApiTestData.Preview(cars);
        input["rules"]!["hardRules"] = JsonNode.Parse("""
            [{"criterionKey":"towBar","operator":"equals","minimumEvidence":"userConfirmed","allowedValues":[{"boolean":true}]},
             {"criterionKey":"purchasePriceSek","operator":"inclusiveRange","minimumEvidence":"userConfirmed","minimum":40000,"maximum":40000},
             {"criterionKey":"odometerKilometres","operator":"inclusiveRange","minimumEvidence":"userConfirmed","maximum":200000}]
            """);
        var result = await Json(await ComparisonApiTestData.Post(client, input));
        Assert.Equal(new[] { 1, 0, 2, 3 }.Select(i => cars[i]["vehicleId"]!.GetValue<Guid>()),
            result["costOrder"]!.AsArray().Select(x => x!.GetValue<Guid>()));
        Assert.True(result["candidates"]![1]!["isCheapestEligibleComplete"]!.GetValue<bool>());
        Assert.Equal("rejected", result["candidates"]![3]!["eligibility"]!.GetValue<string>());
        input["rules"]!["hardRules"]!.AsArray().Add(JsonNode.Parse("""
            {"criterionKey":"ownerCount","operator":"inclusiveRange","minimumEvidence":"userConfirmed","maximum":3}
            """));
        result = await Json(await ComparisonApiTestData.Post(client, input));
        Assert.Equal("needsVerification", result["candidates"]![0]!["eligibility"]!.GetValue<string>());
        Assert.False(result["candidates"]![1]!["isCheapestEligibleComplete"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Presentation_rounding_does_not_change_cost_order_or_budget_decision()
    {
        using var factory = new ManualCalculationApiFactory(); using var client = factory.CreateClient();
        var cars = new[] { ComparisonApiTestData.Candidate("AAA001"), ComparisonApiTestData.Candidate("ZZZ999") };
        for (var i = 0; i < 2; i++)
        {
            cars[i]["costInput"] = Vehicle();
            cars[i]["costInput"]!["residual"]!["value"]!["single"] = i == 0 ? 40000.001m : 40000.002m;
            cars[i]["costInput"]!["customCosts"]!["items"] = JsonNode.Parse("""
                [{"key":"fraction","label":"Monthly","amountSek":{"single":0.001},"cadence":"monthly"}]
                """);
            cars[i]["facts"]!["costConfirmation"] = "confirm";
        }
        var result = await Json(await ComparisonApiTestData.Post(client, ComparisonApiTestData.Preview(cars)));
        Assert.Equal(cars[1]["vehicleId"]!.GetValue<Guid>(), result["costOrder"]![0]!.GetValue<Guid>());
        var a = result["candidates"]![0]!["cost"]!;
        var b = result["candidates"]![1]!["cost"]!;
        Assert.Equal(a["totals"]!["ownershipCost"]!["completeTotalSek"]!.GetValue<decimal>(),
            b["totals"]!["ownershipCost"]!["completeTotalSek"]!.GetValue<decimal>());
        Assert.Equal("exceeded", a["monthlyBudget"]!["status"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("automatic", 3, 2, 85, 85, 100)]
    [InlineData(null, 3, 2, 45, 85, 60)]
    [InlineData(null, 3, 0, 75, 75, 100)]
    [InlineData("manual", 3, 2, 45, 45, 100)]
    [InlineData("manual", 1, 4, 15, 15, 100)]
    public async Task B1_to_B4_scores_use_common_weights_and_explicit_manual_evidence(string? gearbox, int pw, int gw, decimal lower, decimal upper, decimal coverage)
    {
        using var factory = new ManualCalculationApiFactory(); using var client = factory.CreateClient();
        var input = ComparisonApiTestData.Preview(ComparisonApiTestData.Candidate(gearbox: gearbox));
        input["rules"] = ComparisonApiTestData.Rules(pw, gw);
        var result = await Json(await ComparisonApiTestData.Post(client, input));
        Assert.False(result["storageChecked"]!.GetValue<bool>());
        var car = result["candidates"]![0]!;
        Assert.Equal(lower, car["score"]!["lower"]!.GetValue<decimal>());
        Assert.Equal(upper, car["score"]!["upper"]!.GetValue<decimal>());
        Assert.Equal(coverage, car["coveragePercent"]!.GetValue<decimal>());
        Assert.Equal(1, result["ruleVersion"]!.GetValue<int>()); Assert.Equal(2, result["calculationVersion"]!.GetValue<int>());
    }

    [Fact]
    public async Task B5_overlap_B8_independence_and_zero_active_preferences()
    {
        using var factory = new ManualCalculationApiFactory(); using var client = factory.CreateClient();
        var a = ComparisonApiTestData.Candidate("ABC123", 40000, null);
        var b = ComparisonApiTestData.Candidate("DEF456", 20000, null);
        var input = ComparisonApiTestData.Preview(a, b);
        input["rules"] = JsonNode.Parse("""
          {"preferences":[
          {"criterionKey":"purchasePriceSek","weight":3,"minimumEvidence":"userConfirmed","zeroPoint":100000,"fullPoint":20000},
          {"criterionKey":"transmission","weight":1,"minimumEvidence":"userConfirmed","preferredValues":[{"transmission":"automatic"}]},
          {"criterionKey":"towBar","weight":1,"minimumEvidence":"userConfirmed","preferredValues":[{"boolean":true}]}]}
          """);
        b["facts"]!["edits"]!["towBar"] = ComparisonApiTestData.Manual(false);
        var result = await Json(await ComparisonApiTestData.Post(client, input));
        Assert.Equal(45, result["candidates"]![0]!["score"]!["lower"]!.GetValue<decimal>());
        Assert.Equal(85, result["candidates"]![0]!["score"]!["upper"]!.GetValue<decimal>());
        Assert.Equal(60, result["candidates"]![1]!["score"]!["lower"]!.GetValue<decimal>());
        Assert.Equal(80, result["candidates"]![1]!["score"]!["upper"]!.GetValue<decimal>());
        Assert.Equal(b["vehicleId"]!.GetValue<Guid>(), result["scoreOrder"]![0]!.GetValue<Guid>());
        Assert.Equal("overlapOrTie", result["preferenceRecommendationReason"]!.GetValue<string>());
        var before = result["candidates"]![0]!["contributions"]![0]!["range"]!.ToJsonString();
        input["candidates"]!.AsArray().RemoveAt(1);
        var single = await Json(await ComparisonApiTestData.Post(client, input));
        Assert.Equal(before, single["candidates"]![0]!["contributions"]![0]!["range"]!.ToJsonString());
        input["rules"] = ComparisonApiTestData.Rules(0, 0);
        var zero = await Json(await ComparisonApiTestData.Post(client, input));
        Assert.Null(zero["candidates"]![0]!["score"]); Assert.Null(zero["candidates"]![0]!["coveragePercent"]);
    }

    [Fact]
    public async Task Invalid_independent_values_preserve_a_safe_hard_failure_and_other_candidates()
    {
        using var factory = new ManualCalculationApiFactory(); using var client = factory.CreateClient();
        var input = ComparisonApiTestData.Preview(ComparisonApiTestData.Candidate(), ComparisonApiTestData.Candidate("DEF456"));
        input["profile"]!["annualDistanceKilometres"] = -1;
        input["candidates"]![0]!["facts"]!["edits"]!["seats"] = ComparisonApiTestData.Manual(0);
        input["candidates"]![0]!["facts"]!["edits"]!["towBar"] = ComparisonApiTestData.Manual(false);
        input["rules"]!["hardRules"] = JsonNode.Parse("""[{"criterionKey":"towBar","operator":"equals","minimumEvidence":"userConfirmed","allowedValues":[{"boolean":true}]}]""");
        var result = await Json(await ComparisonApiTestData.Post(client, input));
        Assert.Equal("rejected", result["candidates"]![0]!["eligibility"]!.GetValue<string>());
        Assert.NotEmpty(result["candidates"]![0]!["errors"]!.AsArray()); Assert.DoesNotContain(result["candidates"]![1]!["errors"]!.AsArray(), x => x!["path"]!.GetValue<string>().Contains("facts"));
        Assert.NotEmpty(result["profileErrors"]!.AsArray());
    }

    [Theory]
    [InlineData("registry")]
    [InlineData("userConfirmed")]
    public async Task Client_verification_fields_are_not_a_write_contract(string claim)
    {
        using var factory = new ManualCalculationApiFactory(); using var client = factory.CreateClient();
        var input = ComparisonApiTestData.Preview(ComparisonApiTestData.Candidate());
        input["candidates"]![0]!["facts"]!["edits"]!["purchasePriceSek"]!["verification"] = claim;
        var problem = await Json(await ComparisonApiTestData.Post(client, input), HttpStatusCode.BadRequest);
        Assert.Equal("invalidComparisonInput", problem["code"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(0, 200)]
    [InlineData(100, 200)]
    [InlineData(101, 400)]
    public async Task Candidate_count_is_bounded(int count, int status)
    {
        using var factory = new ManualCalculationApiFactory(); using var client = factory.CreateClient();
        var cars = Enumerable.Range(0, count).Select(i => ComparisonApiTestData.Candidate($"ABC{i:000}")).ToArray();
        await Json(await ComparisonApiTestData.Post(client, ComparisonApiTestData.Preview(cars)), (HttpStatusCode)status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Body_limit_is_applied_even_without_content_length(bool chunked)
    {
        using var factory = new ManualCalculationApiFactory(); using var client = factory.CreateClient();
        var body = ComparisonApiTestData.Preview().ToJsonString();
        foreach (var extra in new[] { 0, 1 })
        {
            var text = body + new string(' ', 2 * 1024 * 1024 - System.Text.Encoding.UTF8.GetByteCount(body) + extra);
            using HttpContent content = chunked ? new ChunkedContent(text) : new StringContent(text);
            content.Headers.ContentType = new("application/json");
            await Json(await client.PostAsync("/api/comparisons/preview", content), extra == 0 ? HttpStatusCode.OK : HttpStatusCode.RequestEntityTooLarge);
        }
    }

    [Fact]
    public async Task Manual_mode_never_resolves_comparison_or_household_stores()
    {
        using var factory = new ManualCalculationApiFactory();
        using var isolated = factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
        {
            services.RemoveAll<IComparisonSnapshotStore>(); services.RemoveAll<IRuleProfileStore>(); services.RemoveAll<IVehicleFactsStore>();
            services.AddScoped<IComparisonSnapshotStore>(_ => throw new InvalidOperationException("Do not resolve storage."));
            services.AddScoped<IRuleProfileStore>(_ => throw new InvalidOperationException("Do not resolve storage."));
            services.AddScoped<IVehicleFactsStore>(_ => throw new InvalidOperationException("Do not resolve storage."));
        }));
        using var client = isolated.CreateClient();
        await Json(await ComparisonApiTestData.Post(client, ComparisonApiTestData.Preview(ComparisonApiTestData.Candidate())));
    }
}
