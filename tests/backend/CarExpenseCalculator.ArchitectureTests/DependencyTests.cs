using CarExpenseCalculator.CodexExtractor;
using CarExpenseCalculator.Core;
using CarExpenseCalculator.Extraction.Contracts;
using CarExpenseCalculator.Infrastructure;
using Xunit;

namespace CarExpenseCalculator.ArchitectureTests;

public sealed class DependencyTests
{
    [Fact]
    public void Core_does_not_reference_outer_layers()
    {
        var references = typeof(CoreAssemblyMarker).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.DoesNotContain("CarExpenseCalculator.Api", references);
        Assert.DoesNotContain("CarExpenseCalculator.Infrastructure", references);
    }

    [Fact]
    public void Infrastructure_does_not_reference_api()
    {
        var references = typeof(InfrastructureAssemblyMarker).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.DoesNotContain("CarExpenseCalculator.Api", references);
        Assert.DoesNotContain("CarExpenseCalculator.CodexExtractor", references);
    }

    [Fact]
    public void Extraction_contracts_do_not_reference_application_layers()
    {
        var references = typeof(ExtractionContractsAssemblyMarker).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.DoesNotContain("CarExpenseCalculator.Api", references);
        Assert.DoesNotContain("CarExpenseCalculator.Core", references);
        Assert.DoesNotContain("CarExpenseCalculator.Infrastructure", references);
        Assert.DoesNotContain("CarExpenseCalculator.CodexExtractor", references);
    }

    [Fact]
    public void Codex_extractor_does_not_reference_api_infrastructure_or_database_packages()
    {
        var references = typeof(CodexExtractorAssemblyMarker).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.DoesNotContain("CarExpenseCalculator.Api", references);
        Assert.DoesNotContain("CarExpenseCalculator.Infrastructure", references);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", references);
        Assert.DoesNotContain("Npgsql", references);
    }
}
