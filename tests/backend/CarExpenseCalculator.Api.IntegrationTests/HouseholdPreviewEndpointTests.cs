using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;
using static CarExpenseCalculator.Api.IntegrationTests.HouseholdApiTestData;

namespace CarExpenseCalculator.Api.IntegrationTests;

public sealed class HouseholdPreviewEndpointTests(ManualCalculationApiFactory factory) : IClassFixture<ManualCalculationApiFactory>
{
    [Fact]
    public async Task Complete_purchase_without_database_echoes_inputs_and_request_id()
    {
        using var client = factory.CreateClient();
        var request = Preview();
        request["vehicles"]![0]!["input"]!["candidateKey"] = " car ";
        var response = await Json(await Post(client, request));
        Assert.Equal("generation-1", response["requestId"]!.GetValue<string>());
        Assert.Equal(2, response["calculationVersion"]!.GetValue<int>());
        Assert.Equal(2, response["resultSchemaVersion"]!.GetValue<int>());
        Assert.Equal("car", response["vehicles"]![0]!["input"]!["candidateKey"]!.GetValue<string>());
        Assert.True(response["vehicles"]![0]!["isCostComparable"]!.GetValue<bool>());
        var result = Sections(response);
        Assert.Equal(10000m, result["totals"]!["ownershipCost"]!["completeTotalSek"]!.GetValue<decimal>());
        Assert.Equal(833.33m, result["totals"]!["monthlyCost"]!["completeTotalSek"]!.GetValue<decimal>());
        Assert.Equal("notApplicable", result["lease"]!["cost"]!["state"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("priceSek", -1)]
    [InlineData("priceSek", 100000001)]
    public async Task Numeric_error_preserves_other_candidates_and_independent_sections(string field, int amount)
    {
        using var client = factory.CreateClient();
        var request = Preview();
        request["vehicles"] = new JsonArray(Candidate(Vehicle("bad").With(field, JsonValue.Create(amount)), "ABC123"),
            Candidate(Vehicle("good"), "DEF456"));
        var response = await Json(await Post(client, request));
        var bad = Sections(response);
        Assert.Equal("invalid", bad["financing"]!["state"]!.GetValue<string>());
        Assert.Equal("complete", bad["tax"]!["cost"]!["state"]!.GetValue<string>());
        Assert.Contains("vehicles[0].input.priceSek", bad["inputErrors"]!.ToJsonString());
        Assert.True(response["vehicles"]![1]!["isCostComparable"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("zero")]
    [InlineData("included")]
    public async Task Missing_zero_and_included_categories_are_distinct(string mode)
    {
        using var client = factory.CreateClient();
        var input = Vehicle();
        if (mode == "unknown") input["tax"] = null;
        if (mode == "included") input["tax"]!["isIncluded"] = true;
        var response = await Json(await Post(client, Preview(input)));
        var tax = Sections(response)["tax"]!;
        Assert.Equal(mode == "included", tax["isIncluded"]!.GetValue<bool>());
        Assert.Equal(mode != "unknown", tax["cost"]!["completeTotalSek"] is not null);
        Assert.Equal(0m, tax["cost"]!["knownSubtotalSek"]!.GetValue<decimal>());
    }

    [Fact]
    public async Task Lease_A9_and_longer_horizon_keep_cashflows_and_partial_coverage()
    {
        using var client = factory.CreateClient();
        var profile = Profile().With("periodMonths", JsonValue.Create(24));
        var request = Preview(Lease(), profile);
        var response = await Json(await Post(client, request));
        var result = Sections(response);
        Assert.Equal(60000m, result["totals"]!["ownershipCost"]!["completeTotalSek"]!.GetValue<decimal>());
        Assert.Equal(63000m, result["payments"]!["externalOutflow"]!["completeTotalSek"]!.GetValue<decimal>());
        Assert.Equal(3000m, result["payments"]!["externalInflow"]!["completeTotalSek"]!.GetValue<decimal>());
        Assert.Equal(9000m, result["startupBudget"]!["fundingRequired"]!["completeTotalSek"]!.GetValue<decimal>());
        request["profile"]!["periodMonths"] = 36;
        result = Sections(await Json(await Post(client, request)));
        Assert.Null(result["totals"]!["ownershipCost"]!["completeTotalSek"]);
        Assert.Equal(24, result["payments"]!["coveredMonths"]!.GetValue<int>());
        Assert.Contains("leaseHorizonMismatch", result.ToJsonString());
    }

    [Theory]
    [InlineData("baseline", 200)]
    [InlineData("favorable", 100)]
    [InlineData("cautious", 300)]
    public async Task All_sensitivity_values_are_preserved_and_active_mode_is_shared(string mode, int expected)
    {
        using var client = factory.CreateClient();
        var input = Vehicle();
        input["additionalRepairAllowancePerMonthSek"] = JsonNode.Parse("""{"favorable":100,"baseline":200,"cautious":300}""");
        var request = Preview(input, Profile().With("activeSensitivityMode", JsonValue.Create(mode)));
        var result = Sections(await Json(await Post(client, request)));
        Assert.Equal(expected * 12m, result["repairAllowance"]!["completeTotalSek"]!.GetValue<decimal>());
    }

    [Theory]
    [InlineData("maintenance", "service", "startupBudget")]
    [InlineData("oneTime", "customCosts", "startupBudget")]
    [InlineData("energy", "energy", "monthlyBudget")]
    [InlineData("tax", "tax", "monthlyBudget")]
    public async Task Legacy_review_blocks_only_affected_completeness_and_never_adds_amounts(string kind, string category, string budget)
    {
        using var client = factory.CreateClient();
        var request = Preview();
        request["vehicles"]![0]!["unresolvedLegacyItems"] = JsonNode.Parse($$"""[{"key":"old","kind":"{{kind}}","label":"Review","amountSek":9000}]""");
        var response = await Json(await Post(client, request));
        var result = Sections(response);
        Assert.False(response["vehicles"]![0]!["isCostComparable"]!.GetValue<bool>());
        Assert.Null(result["totals"]!["ownershipCost"]!["completeTotalSek"]);
        Assert.Equal(10000m, result["totals"]!["ownershipCost"]!["knownSubtotalSek"]!.GetValue<decimal>());
        Assert.Equal("partial", result[category]!["cost"]!["state"]!.GetValue<string>());
        Assert.Equal("unknown", result[budget]!["status"]!.GetValue<string>());
        Assert.Equal("complete", result["depreciation"]!["cost"]!["state"]!.GetValue<string>());
        Assert.Null(result["reconciliation"]!["reconciledOwnershipCost"]!["completeTotalSek"]);
    }

    [Theory]
    [InlineData(99, "exceeded")]
    [InlineData(100, "unknown")]
    [InlineData(null, "notConfigured")]
    [InlineData(-1, "invalid")]
    public async Task Outstanding_review_preserves_safe_budget_outcomes(int? limit, string expected)
    {
        using var client = factory.CreateClient();
        var request = Preview(Vehicle().With("additionalRepairAllowancePerMonthSek", JsonNode.Parse("""{"single":100}""")),
            Profile().With("monthlyBudgetSek", limit is null ? null : JsonValue.Create(limit.Value)));
        request["vehicles"]![0]!["unresolvedLegacyItems"] = JsonNode.Parse("""[{"key":"old","kind":"energy","label":"Energy"}]""");
        var result = Sections(await Json(await Post(client, request)));
        Assert.Equal(expected, result["monthlyBudget"]!["status"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{broken")]
    [InlineData("{\"requestId\":\"x\",\"profile\":null,\"vehicles\":[]}")]
    public async Task Invalid_envelopes_are_400(string json)
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsync("/api/household-calculations/preview", new StringContent(json, Encoding.UTF8, "application/json"));
        var problem = await Json(response, HttpStatusCode.BadRequest);
        Assert.Equal("invalidHouseholdInput", problem["code"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("enum")]
    [InlineData("enumInteger")]
    [InlineData("decimalString")]
    [InlineData("decimalOverflow")]
    [InlineData("clientResult")]
    [InlineData("sensitivity")]
    [InlineData("nullItem")]
    [InlineData("duplicateKey")]
    [InlineData("duplicateRegistration")]
    [InlineData("missingRegistration")]
    [InlineData("reviewMask")]
    public async Task Structural_input_errors_are_400(string mode)
    {
        using var client = factory.CreateClient();
        var request = Preview();
        var vehicle = request["vehicles"]![0]!["input"]!;
        switch (mode)
        {
            case "enum": vehicle["acquisitionType"] = "other"; break;
            case "enumInteger": vehicle["acquisitionType"] = 0; break;
            case "decimalString": vehicle["priceSek"] = "50000"; break;
            case "decimalOverflow": vehicle["priceSek"] = JsonNode.Parse("1e100"); break;
            case "clientResult": vehicle["totals"] = new JsonObject(); break;
            case "sensitivity": vehicle["additionalRepairAllowancePerMonthSek"] = JsonNode.Parse("""{"baseline":1}"""); break;
            case "nullItem": vehicle["energySources"] = new JsonArray((JsonNode?)null); break;
            case "duplicateKey": request["vehicles"] = new JsonArray(Candidate(Vehicle(), "ABC123"), Candidate(Vehicle(), "DEF456")); break;
            case "duplicateRegistration": request["vehicles"] = new JsonArray(Candidate(Vehicle("a"), "abc 123"), Candidate(Vehicle("b"), "ABC123")); break;
            case "missingRegistration": request["vehicles"] = new JsonArray(Candidate(Vehicle("a")), Candidate(Vehicle("b"))); break;
            case "reviewMask": request["vehicles"]![0]!["unresolvedLegacyItems"] = JsonNode.Parse("""[{"key":"old","kind":"energy","label":"x","affectedSections":[]}]"""); break;
        }
        await Json(await Post(client, request), HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(100, HttpStatusCode.OK)]
    [InlineData(101, HttpStatusCode.BadRequest)]
    public async Task Candidate_limit_is_enforced(int count, HttpStatusCode expected)
    {
        using var client = factory.CreateClient();
        var request = Preview();
        request["vehicles"] = new JsonArray(Enumerable.Range(0, count).Select(i => (JsonNode)Candidate(Vehicle($"car{i}"), $"ABC{i:000}")).ToArray());
        await Json(await Post(client, request), expected);
    }

    [Theory]
    [InlineData(false, 0, HttpStatusCode.OK)]
    [InlineData(true, 0, HttpStatusCode.OK)]
    [InlineData(false, 1, HttpStatusCode.RequestEntityTooLarge)]
    [InlineData(true, 1, HttpStatusCode.RequestEntityTooLarge)]
    public async Task Body_limit_includes_chunked_and_exact_boundary(bool chunked, int extra, HttpStatusCode expected)
    {
        using var client = factory.CreateClient();
        var json = Preview().ToJsonString().PadRight(2 * 1024 * 1024 + extra);
        using HttpContent content = chunked ? new ChunkedContent(json) : new StringContent(json, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        await Json(await client.PostAsync("/api/household-calculations/preview", content), expected);
    }

    [Fact]
    public async Task Rounded_display_does_not_change_budget_or_review_exceedance()
    {
        using var client = factory.CreateClient();
        var request = Preview(Vehicle().With("additionalRepairAllowancePerMonthSek", JsonNode.Parse("""{"single":0.004}""")));
        request["vehicles"]![0]!["unresolvedLegacyItems"] = JsonNode.Parse("""[{"key":"old","kind":"energy","label":"Energy"}]""");
        var budget = Sections(await Json(await Post(client, request)))["monthlyBudget"]!;
        Assert.Equal(0m, budget["fundingRequired"]!["knownSubtotalSek"]!.GetValue<decimal>());
        Assert.Equal("exceeded", budget["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Known_kg_quantity_survives_missing_price_and_zero_distance_keeps_costs()
    {
        using var client = factory.CreateClient();
        var request = Preview();
        request["vehicles"]![0]!["input"]!["energySources"] = JsonNode.Parse("""[{"key":"gas","fuel":"biogas","unit":"kilogram","consumptionPer100Kilometres":{"single":4},"consumptionBasis":"wholeDistance"}]""");
        var result = Sections(await Json(await Post(client, request)));
        Assert.Equal(480m, result["energy"]!["sources"]![0]!["purchasedQuantity"]!.GetValue<decimal>());
        Assert.Null(result["energy"]!["cost"]!["completeTotalSek"]);
        request["profile"]!["annualDistanceKilometres"] = 0;
        result = Sections(await Json(await Post(client, request)));
        Assert.Equal(10000m, result["totals"]!["ownershipCost"]!["completeTotalSek"]!.GetValue<decimal>());
        Assert.Null(result["totals"]!["costPerMil"]!["completeTotalSek"]);
    }

    [Theory]
    [InlineData("energySources", 3)]
    [InlineData("tax", 51)]
    [InlineData("lease", 121)]
    public async Task Core_collection_limits_are_http_structure_errors(string section, int count)
    {
        using var client = factory.CreateClient();
        var input = section == "lease" ? Lease() : Vehicle();
        if (section == "energySources") input[section] = new JsonArray(Enumerable.Range(0, count)
            .Select(i => (JsonNode)new JsonObject { ["key"] = $"energy{i}" }).ToArray());
        else if (section == "tax") input["tax"]!["items"] = new JsonArray(Enumerable.Range(0, count)
            .Select(i => (JsonNode)new JsonObject { ["key"] = $"tax{i}", ["label"] = "Tax" }).ToArray());
        else input["lease"]!["monthlyPayments"] = new JsonArray(Enumerable.Range(1, count)
            .Select(i => (JsonNode)new JsonObject { ["monthOffset"] = i }).ToArray());
        await Json(await Post(client, Preview(input)), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Duplicate_cost_keys_and_reused_unresolved_sources_cannot_double_count()
    {
        using var client = factory.CreateClient();
        var request = Preview();
        request["vehicles"]![0]!["input"]!["tax"]!["items"] = JsonNode.Parse("""[{"key":"old","label":"Tax","amountSek":{"single":1},"cadence":"monthly"}]""");
        request["vehicles"]![0]!["input"]!["insurance"]!["items"] = request["vehicles"]![0]!["input"]!["tax"]!["items"]!.DeepClone();
        await Json(await Post(client, request), HttpStatusCode.BadRequest);
        request["vehicles"]![0]!["input"]!["insurance"]!["items"] = new JsonArray();
        request["vehicles"]![0]!["unresolvedLegacyItems"] = JsonNode.Parse("""[{"key":"old","kind":"tax","label":"Tax"}]""");
        await Json(await Post(client, request), HttpStatusCode.BadRequest);
    }
}
