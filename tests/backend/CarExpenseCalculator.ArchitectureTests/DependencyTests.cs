using CarExpenseCalculator.Core;
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
    }
}
