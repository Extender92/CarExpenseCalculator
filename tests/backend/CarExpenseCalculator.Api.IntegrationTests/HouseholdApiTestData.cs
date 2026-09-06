using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace CarExpenseCalculator.Api.IntegrationTests;

internal static class HouseholdApiTestData
{
    public static JsonObject Profile() => JsonNode.Parse("""
        {"startMonth":{"year":2026,"month":1},"periodMonths":12,"annualDistanceKilometres":12000,
         "purchaseCashSek":50000,"startupBudgetSek":0,"monthlyBudgetSek":0,
         "energyPrices":[{"fuel":"petrol","unit":"litre","pricePerUnitSek":{"single":0}}]}
        """)!.AsObject();
    public static JsonObject Vehicle(string key = "car") => JsonNode.Parse("""
        {"candidateKey":"car","priceSek":50000,"residual":{"mode":"fixedAmount","value":{"single":40000},"periodMonths":12},
         "energySources":[{"key":"petrol","fuel":"petrol","unit":"litre","consumptionPer100Kilometres":{"single":5},"consumptionBasis":"wholeDistance"}],
         "tax":{"isIncluded":false,"items":[]},"insurance":{"isIncluded":false,"items":[]},
         "service":{"isIncluded":false,"items":[]},"repairs":{"isIncluded":false,"items":[]},
         "additionalRepairAllowancePerMonthSek":{"single":0},"customCosts":{"isIncluded":false,"items":[]}}
        """)!.AsObject().With("candidateKey", JsonValue.Create(key));
    public static JsonObject Lease()
    {
        var vehicle = Vehicle();
        vehicle.Remove("priceSek"); vehicle.Remove("residual");
        vehicle["acquisitionType"] = "lease";
        vehicle["lease"] = new JsonObject
        {
            ["termMonths"] = 24, ["upfrontNonRefundableSek"] = 6000, ["refundableDepositSek"] = 3000,
            ["depositRefundSek"] = new JsonObject { ["single"] = 3000 }, ["includedDistanceKilometres"] = 24000,
            ["priceBasis"] = "quoted", ["energyIncluded"] = true, ["endFees"] = new JsonArray(), ["otherPayments"] = new JsonArray(),
            ["monthlyPayments"] = new JsonArray(Enumerable.Range(1, 24).Select(i => (JsonNode)new JsonObject
                { ["monthOffset"] = i, ["amountSek"] = 2250 }).ToArray()),
        };
        return vehicle;
    }
    public static JsonObject Preview(JsonObject? vehicle = null, JsonObject? profile = null) => new()
    {
        ["requestId"] = "generation-1", ["profile"] = profile ?? Profile(),
        ["vehicles"] = new JsonArray(Candidate(vehicle ?? Vehicle())),
    };
    public static JsonObject Candidate(JsonObject input, string? registration = null) => new()
    { ["input"] = input, ["registrationNumber"] = registration, ["unresolvedLegacyItems"] = new JsonArray() };
    public static JsonObject Create(string registration = "ABC123", JsonObject? input = null) => new()
    { ["registrationNumber"] = registration, ["cost"] = new JsonObject { ["input"] = input ?? Vehicle() } };
    public static JsonObject With(this JsonObject obj, string key, JsonNode? value) { obj[key] = value; return obj; }
    public static async Task<JsonNode> Json(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"Expected {(int)expected}, got {(int)response.StatusCode}: {text}");
        return JsonNode.Parse(text)!;
    }
    public static JsonNode Sections(JsonNode preview) => preview["vehicles"]![0]!["sections"]!;
    public static Task<HttpResponseMessage> Post(HttpClient client, JsonNode request) => client.PostAsJsonAsync("/api/household-calculations/preview", request);

    internal sealed class ChunkedContent(string text) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(Encoding.UTF8.GetBytes(text)).AsTask();
    }
}
