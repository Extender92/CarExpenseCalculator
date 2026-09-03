using CarExpenseCalculator.Infrastructure.ListingExtraction;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CarExpenseCalculator.Infrastructure.UnitTests;

public sealed class ListingExtractionRegistrationTests
{
    [Fact]
    public void Typed_client_uses_the_private_sidecar_and_65_second_transport_timeout()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Postgres"] = "Host=unused;Database=unused;Username=unused;Password=unused",
                    ["CodexExtraction:BaseUrl"] = "http://codex-extractor:8080",
                })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();
        var service = Assert.IsType<CodexListingExtractionService>(
            provider.GetRequiredService<IListingExtractionService>());
        var clientField = typeof(CodexListingExtractionService)
            .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Single(field => field.FieldType == typeof(HttpClient));
        var client = Assert.IsType<HttpClient>(clientField.GetValue(service));

        Assert.Equal(new Uri("http://codex-extractor:8080"), client.BaseAddress);
        Assert.Equal(TimeSpan.FromSeconds(65), client.Timeout);
    }
}
