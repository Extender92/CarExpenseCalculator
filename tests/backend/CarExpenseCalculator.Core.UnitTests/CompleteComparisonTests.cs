using System.Text.Json;
using CarExpenseCalculator.Core.Comparisons;
using CarExpenseCalculator.Core.Households;
using Xunit;

namespace CarExpenseCalculator.Core.UnitTests;

public sealed class CompleteComparisonTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(101)]
    [InlineData(250)]
    public void Complete_membership_and_results_are_independent_of_processing_groups(int count)
    {
        var cars = Enumerable.Range(100, count).Select(i => Examples.Candidate(i, Examples.B1Facts(), Examples.Cost(i, i % 9))).ToArray();
        var engine = new ComparisonEvaluator();
        var rules = new RuleProfileInput(preferences: [new("netCostSek", 1, EvidenceRequirement.Advertised, 200, 0)]);
        var result = engine.EvaluateAllComparison(Examples.Profile(), rules, Examples.Date, cars);
        Assert.Equal(count, result.Candidates.Count);
        Assert.Equal(count, result.CostOrder.Distinct().Count());
        Assert.Equal(count, result.ScoreOrder.Distinct().Count());
        foreach (var size in new[] { 1, 7, 99 })
            Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(engine.EvaluateBatches(
                Examples.Profile(), rules, Examples.Date, cars, size, CancellationToken.None)));
        if (count <= 100)
            Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(engine.EvaluateComparison(Examples.Profile(), rules, Examples.Date, cars)));
    }

    [Fact]
    public void Cross_group_cost_and_score_differences_survive_display_rounding()
    {
        var cars = Enumerable.Range(100, 101).Select(i => Examples.Candidate(i, cost: Examples.Cost(i, 150))).ToArray();
        cars[0] = Examples.Candidate(100, cost: Examples.Cost(100, 100.002m));
        cars[100] = Examples.Candidate(200, cost: Examples.Cost(200, 100.001m));
        var result = new ComparisonEvaluator().EvaluateAllComparison(Examples.Profile(),
            new(preferences: [new("netCostSek", 1, EvidenceRequirement.Advertised, 200, 0)]), Examples.Date, cars);
        Assert.Equal(result.Candidates[0].Score, result.Candidates[100].Score);
        Assert.Equal(100m, result.Candidates[100].Cost.Totals.OwnershipCost.CompleteTotalSek);
        Assert.Equal(cars[100].VehicleId, result.CostOrder[0]);
        Assert.Equal(cars[100].VehicleId, result.ScoreOrder[0]);
        Assert.True(result.Candidates[100].IsDefinitePreferenceWinner);
        Assert.Equal(cars[100].VehicleId, Assert.Single(result.Candidates, x => x.IsCheapestEligibleComplete).EffectiveInput.VehicleId);
    }

    [Fact]
    public void Cross_group_exact_ties_and_overlap_never_become_local_winners()
    {
        var cars = Enumerable.Range(100, 101).Select(i => Examples.Candidate(i,
            Examples.B1Facts() with { TowBar = Examples.Known(false) }, Examples.Cost(i, 50))).ToArray();
        cars[0] = Examples.Candidate(100, Examples.Tow(true), Examples.Cost(100, 10));
        cars[100] = Examples.Candidate(200, Examples.Tow(true), Examples.Cost(200, 10));
        var result = new ComparisonEvaluator().EvaluateAllComparison(Examples.Profile(),
            new([Examples.TowRule()], [new("netCostSek", 1, EvidenceRequirement.Advertised, 100, 0)]), Examples.Date, cars);
        Assert.Equal(2, result.Candidates.Count(x => x.IsCheapestEligibleComplete));
        Assert.All(result.Candidates, x => Assert.False(x.IsDefinitePreferenceWinner));
        Assert.Equal("overlapOrTie", result.PreferenceRecommendationReason);
    }

    [Fact]
    public void Independent_numeric_errors_after_group_boundary_use_global_paths()
    {
        var cars = Enumerable.Range(100, 101).Select(i => Examples.Candidate(i, cost: Examples.Cost(i, 50))).ToArray();
        cars[100] = Examples.Candidate(200, Examples.Tow(false) with { Seats = Examples.Known(0) }, Examples.Cost(200, 50) with { PriceSek = -1 });
        var result = new ComparisonEvaluator().EvaluateAllComparison(Examples.Profile(), new([Examples.TowRule()]), Examples.Date, cars);
        var invalid = result.Candidates[100];
        Assert.Equal(BuyingEligibility.Rejected, invalid.Eligibility);
        Assert.Contains(invalid.Errors, x => x.Path == "candidates[100].costInput.priceSek");
        Assert.Contains(invalid.Errors, x => x.Path.StartsWith("candidates[100].facts.seats", StringComparison.Ordinal));
        Assert.Contains(invalid.Cost.Financing!.Errors, x => x.Path == "vehicles[100].priceSek");
        Assert.NotNull(result.Candidates[0].Cost.Totals.OwnershipCost.CompleteTotalSek);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Duplicates_after_group_boundary_are_rejected(bool identity)
    {
        var cars = Enumerable.Range(100, 101).Select(i => Examples.Candidate(i)).ToArray();
        var original = cars[0];
        cars[100] = new(identity ? original.VehicleId : cars[100].VehicleId, original.RegistrationNumber, original.Facts);
        var error = Assert.Throws<ComparisonInputValidationException>(() => new ComparisonEvaluator().EvaluateAllComparison(
            Examples.Profile(), new(), Examples.Date, cars));
        Assert.Contains(error.Errors, x => x.Path.StartsWith("candidates[100]", StringComparison.Ordinal) && x.Code == "duplicateKey");
    }

    [Fact]
    public void Cancelled_complete_evaluation_does_not_return_a_subset()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => new ComparisonEvaluator().EvaluateAllComparison(
            Examples.Profile(), new(), Examples.Date, [Examples.B1()], cancellation.Token));
    }
}
