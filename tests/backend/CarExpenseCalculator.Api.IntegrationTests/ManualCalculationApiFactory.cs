using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CarExpenseCalculator.Api.IntegrationTests;

public sealed class ManualCalculationApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(
            "ConnectionStrings:Postgres",
            "Host=127.0.0.1;Port=1;Database=unreachable;Username=unreachable;Password=unreachable;Timeout=1");
    }
}
