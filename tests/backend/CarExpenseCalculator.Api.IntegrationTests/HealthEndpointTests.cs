using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CarExpenseCalculator.Api.IntegrationTests;

public sealed class HealthEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Live_endpoint_is_healthy()
    {
        var response = await _client.GetAsync("/api/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("healthy", payload.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Ready_endpoint_reports_postgres_as_healthy()
    {
        var response = await _client.GetAsync("/api/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("healthy", payload.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task System_status_exposes_foundation_features_as_disabled()
    {
        var response = await _client.GetAsync("/api/system/status");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<SystemStatusContract>();

        Assert.NotNull(payload);
        Assert.Equal("healthy", payload.Status);
        Assert.Equal("available", payload.Database);
        Assert.False(payload.Features.RuleBasedSearch);
        Assert.False(payload.Features.UrlAnalysis);
        Assert.False(payload.Features.ManualCalculator);
        Assert.False(payload.Features.AiReview);
    }

    private sealed record SystemStatusContract(
        string Status,
        string Version,
        string Database,
        FeatureStatusContract Features);

    private sealed record FeatureStatusContract(
        bool RuleBasedSearch,
        bool UrlAnalysis,
        bool ManualCalculator,
        bool AiReview);
}
