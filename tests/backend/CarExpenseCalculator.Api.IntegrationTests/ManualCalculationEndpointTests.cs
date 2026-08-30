using System.Net;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace CarExpenseCalculator.Api.IntegrationTests;

public sealed class ManualCalculationEndpointTests(ManualCalculationApiFactory factory)
    : IClassFixture<ManualCalculationApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Complete_calculation_returns_the_documented_result_without_postgres()
    {
        const string request = """
            {
              "vehicleLabel": " Example car ",
              "calculationPeriodMonths": 12,
              "purchasePriceSek": 20000,
              "expectedResidualValueSek": 15000,
              "annualDistanceKilometres": 15000,
              "financing": {
                "downPaymentSek": 5000,
                "annualNominalInterestRatePercent": 0,
                "termMonths": 12
              },
              "energySources": [
                {
                  "label": " Petrol ",
                  "unit": "litre",
                  "consumptionPer100Kilometres": 8,
                  "pricePerUnitSek": 20,
                  "distanceSharePercent": 100
                }
              ],
              "vehicleTax": { "amountSek": 2400, "cadence": "annual" },
              "insurance": { "amountSek": 500, "cadence": "monthly" },
              "maintenanceAndRepairs": { "amountSek": 6000, "cadence": "annual" },
              "otherRecurringCosts": [
                { "label": " Parking ", "amountSek": 300, "cadence": "monthly" }
              ],
              "otherOneTimeCosts": [
                { "label": " Initial repair ", "amountSek": 2000 }
              ]
            }
            """;

        using var response = await PostAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(MediaTypeNames.Application.Json, response.Content.Headers.ContentType?.MediaType);

        using var payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = payload.RootElement;
        Assert.Equal("SEK", root.GetProperty("currency").GetString());
        Assert.Equal(12, root.GetProperty("calculationPeriodMonths").GetInt32());
        Assert.Equal(15_000m, root.GetProperty("totalDistanceKilometres").GetDecimal());

        var cashFlow = root.GetProperty("cashFlow");
        Assert.Equal(44_000m, cashFlow.GetProperty("knownOperatingCostSek").GetDecimal());
        Assert.Equal(64_000m, cashFlow.GetProperty("knownTotalSek").GetDecimal());

        var energySource = root.GetProperty("energy").GetProperty("sources")[0];
        Assert.Equal("Petrol", energySource.GetProperty("label").GetString());
        Assert.Equal("litre", energySource.GetProperty("unit").GetString());
        Assert.Equal(1_200m, energySource.GetProperty("consumedQuantity").GetDecimal());

        var netOwnershipCost = root.GetProperty("netOwnershipCost");
        Assert.Equal(49_000m, netOwnershipCost.GetProperty("knownTotalSek").GetDecimal());
        Assert.False(root.TryGetProperty("id", out _));
        Assert.False(root.TryGetProperty("timestamp", out _));
    }

    [Fact]
    public async Task Explicit_unknown_costs_return_an_incomplete_known_subtotal()
    {
        const string request = """
            {
              "calculationPeriodMonths": 12,
              "purchasePriceSek": 10000,
              "annualDistanceKilometres": 0,
              "energySources": [],
              "vehicleTax": null,
              "insurance": { "amountSek": 0, "cadence": "annual" },
              "maintenanceAndRepairs": null,
              "otherRecurringCosts": [],
              "otherOneTimeCosts": []
            }
            """;

        using var response = await PostAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = payload.RootElement;
        var completeness = root.GetProperty("completeness");
        Assert.False(completeness.GetProperty("isComplete").GetBoolean());
        Assert.False(completeness.GetProperty("isCashFlowComplete").GetBoolean());
        Assert.False(completeness.GetProperty("isNetOwnershipCostAvailable").GetBoolean());
        Assert.Equal(
            ["vehicleTax", "maintenanceAndRepairs", "residualValue"],
            completeness
                .GetProperty("missingCategories")
                .EnumerateArray()
                .Select(item => item.GetString()!)
                .ToArray());

        var cashFlow = root.GetProperty("cashFlow");
        Assert.Equal(JsonValueKind.Null, cashFlow.GetProperty("vehicleTaxSek").ValueKind);
        Assert.Equal(0m, cashFlow.GetProperty("insuranceSek").GetDecimal());
        Assert.Equal(JsonValueKind.Null, cashFlow.GetProperty("maintenanceAndRepairsSek").ValueKind);
        Assert.Equal(10_000m, cashFlow.GetProperty("knownTotalSek").GetDecimal());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("financing").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("netOwnershipCost").ValueKind);
    }

    [Fact]
    public async Task Semantic_validation_errors_are_grouped_by_core_field_path()
    {
        const string request = """
            {
              "calculationPeriodMonths": 0,
              "purchasePriceSek": -1,
              "expectedResidualValueSek": 2,
              "annualDistanceKilometres": 100,
              "energySources": [],
              "vehicleTax": { "amountSek": -1, "cadence": "annual" },
              "insurance": null,
              "maintenanceAndRepairs": null,
              "otherRecurringCosts": [],
              "otherOneTimeCosts": []
            }
            """;

        using var response = await PostAsync(request);

        var problem = await ReadValidationProblemAsync(response);
        Assert.Equal(
            [
                "calculationPeriodMonths",
                "purchasePriceSek",
                "expectedResidualValueSek",
                "energySources",
                "vehicleTax.amountSek",
            ],
            problem.Errors.Keys);
    }

    [Fact]
    public async Task Missing_required_nullable_property_returns_validation_problem()
    {
        const string request = """
            {
              "calculationPeriodMonths": 12,
              "purchasePriceSek": 10000,
              "annualDistanceKilometres": 0,
              "energySources": [],
              "insurance": null,
              "maintenanceAndRepairs": null,
              "otherRecurringCosts": [],
              "otherOneTimeCosts": []
            }
            """;

        using var response = await PostAsync(request);

        _ = await ReadValidationProblemAsync(response);
    }

    [Fact]
    public async Task Numeric_strings_return_validation_problem()
    {
        const string request = """
            {
              "calculationPeriodMonths": 12,
              "purchasePriceSek": "10000",
              "annualDistanceKilometres": 0,
              "energySources": [],
              "vehicleTax": null,
              "insurance": null,
              "maintenanceAndRepairs": null,
              "otherRecurringCosts": [],
              "otherOneTimeCosts": []
            }
            """;

        using var response = await PostAsync(request);

        _ = await ReadValidationProblemAsync(response);
    }

    [Theory]
    [InlineData("\"gallon\"")]
    [InlineData("0")]
    public async Task Unsupported_or_numeric_energy_unit_returns_validation_problem(string unitJson)
    {
        var request = $$"""
            {
              "calculationPeriodMonths": 12,
              "purchasePriceSek": 10000,
              "annualDistanceKilometres": 1000,
              "energySources": [
                {
                  "label": "Fuel",
                  "unit": {{unitJson}},
                  "consumptionPer100Kilometres": 5,
                  "pricePerUnitSek": 20,
                  "distanceSharePercent": 100
                }
              ],
              "vehicleTax": null,
              "insurance": null,
              "maintenanceAndRepairs": null,
              "otherRecurringCosts": [],
              "otherOneTimeCosts": []
            }
            """;

        using var response = await PostAsync(request);

        _ = await ReadValidationProblemAsync(response);
    }

    private async Task<HttpResponseMessage> PostAsync(string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);
        return await _client.PostAsync("/api/manual-calculations", content);
    }

    private static async Task<ValidationProblemDetails> ReadValidationProblemAsync(
        HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
        Assert.NotEmpty(problem.Errors);

        return problem;
    }
}
