using System.Net;
using System.Net.Http.Json;
using CarExpenseCalculator.Extraction.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CarExpenseCalculator.CodexExtractor.UnitTests;

public sealed class CodexExtractorEndpointTests
{
    [Fact]
    public async Task Internal_endpoints_return_liveness_status_and_extraction()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var live = await client.GetAsync("/health/live");
        var status = await client.GetFromJsonAsync<ListingExtractorStatusResponse>("/internal/status");
        using var extraction = await client.PostAsJsonAsync(
            "/internal/listing-extractions",
            new ListingExtractionRequest(
                "https://example.com/item/1",
                ListingExtractionContractVersions.Prompt,
                ListingExtractionContractVersions.Schema));
        var result = await extraction.Content.ReadFromJsonAsync<ListingExtractionResponse>();

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.True(status!.Configured);
        Assert.Equal(2, status.PromptVersion);
        Assert.Equal(2, status.SchemaVersion);
        Assert.Equal(HttpStatusCode.OK, extraction.StatusCode);
        Assert.Equal("gpt-5.6-luna", result!.RequestedModel);
        Assert.Equal(2, result.PromptVersion);
        Assert.Equal(2, result.SchemaVersion);
        Assert.Equal(["https://example.com/item/1"], result.Sources);
    }

    [Fact]
    public async Task Unsupported_contract_version_returns_typed_problem_details()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/internal/listing-extractions",
            new ListingExtractionRequest("https://example.com/item/1", 1, 1));
        var problem = await response.Content.ReadFromJsonAsync<ListingExtractorProblem>();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(ListingExtractorProblemCodes.UnsupportedVersion, problem!.Code);
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?> { ["CODEX_HOME"] = "/fake/codex-home" }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<CodexExtractorOptions>();
                services.RemoveAll<ICodexProcessRunner>();
                services.AddSingleton(TestData.CreateOptions("/fake/codex-home"));
                services.AddSingleton<ICodexProcessRunner, SuccessfulRunner>();
            });
        });
    }

    private sealed class SuccessfulRunner : ICodexProcessRunner
    {
        public Task<CodexInstallationStatus> GetInstallationStatusAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new CodexInstallationStatus(true, true));

        public Task<CodexProcessResult> RunAsync(
            string host,
            string prompt,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                new CodexProcessResult(
                    0,
                    TestData.SuccessfulJsonl(
                        TestData.EmptyDraftJson(),
                        TestData.WebEvent("open_page", "https://example.com/item/1")),
                    string.Empty,
                    false));
    }
}
