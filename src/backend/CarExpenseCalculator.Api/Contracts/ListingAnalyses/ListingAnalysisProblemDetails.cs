using Microsoft.AspNetCore.Mvc;

namespace CarExpenseCalculator.Api.Contracts.ListingAnalyses;

public sealed class ListingAnalysisProblemDetails : ProblemDetails
{
    public required string Code { get; init; }
}
