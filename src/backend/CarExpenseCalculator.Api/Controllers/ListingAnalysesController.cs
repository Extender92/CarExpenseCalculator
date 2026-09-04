using CarExpenseCalculator.Api.Contracts.ListingAnalyses;
using CarExpenseCalculator.Api.Mapping;
using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Infrastructure.ListingExtraction;
using Microsoft.AspNetCore.Mvc;

namespace CarExpenseCalculator.Api.Controllers;

[ApiController]
[Route("api/listing-analyses")]
public sealed class ListingAnalysesController(IListingExtractionService extractionService) : ControllerBase
{
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<ListingAnalysisResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ListingAnalysisProblemDetails), StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType(typeof(ListingAnalysisProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<ListingAnalysisResponse>> Analyze(
        ListingAnalysisRequest request,
        CancellationToken cancellationToken)
    {
        ListingUrl listingUrl;
        try
        {
            listingUrl = ListingUrl.Parse(request.Url);
        }
        catch (ListingUrlValidationException exception)
        {
            return UrlValidationProblem(exception.Code);
        }

        var outcome = await extractionService.ExtractAsync(listingUrl, cancellationToken);
        return outcome switch
        {
            ListingExtractionSuccess success => Ok(ListingAnalysisMapper.ToApi(success)),
            ListingExtractionFailure failure => ExtractionProblem(failure.Code),
            _ => throw new InvalidOperationException("Unsupported listing extraction outcome."),
        };
    }

    private ActionResult UrlValidationProblem(ListingUrlValidationErrorCode code)
    {
        var message = GetUrlValidationMessage(code);

        return ValidationProblem(
            new ValidationProblemDetails(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["url"] = [message],
                }));
    }

    internal static string GetUrlValidationMessage(ListingUrlValidationErrorCode code) =>
        ListingUrlValidationMessages.Get(code);

    private ObjectResult ExtractionProblem(ListingExtractionFailureCode code)
    {
        return code switch
        {
            ListingExtractionFailureCode.RateLimited => Problem(
                StatusCodes.Status429TooManyRequests,
                "Listing analysis is temporarily rate limited.",
                "listingAnalysisRateLimited"),
            ListingExtractionFailureCode.NotConfigured => Problem(
                StatusCodes.Status503ServiceUnavailable,
                "Listing analysis is not configured.",
                "listingAnalysisNotConfigured"),
            ListingExtractionFailureCode.TimedOut => Problem(
                StatusCodes.Status503ServiceUnavailable,
                "Listing analysis timed out.",
                "listingAnalysisTimedOut"),
            ListingExtractionFailureCode.ProviderUnavailable => Problem(
                StatusCodes.Status503ServiceUnavailable,
                "Listing analysis provider is unavailable.",
                "listingAnalysisProviderUnavailable"),
            ListingExtractionFailureCode.InvalidProviderResponse => Problem(
                StatusCodes.Status503ServiceUnavailable,
                "Listing analysis returned an invalid provider response.",
                "listingAnalysisInvalidProviderResponse"),
            _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unsupported extraction failure."),
        };
    }

    private ObjectResult Problem(int status, string title, string code)
    {
        var problem = new ListingAnalysisProblemDetails
        {
            Type = "about:blank",
            Title = title,
            Status = status,
            Code = code,
        };
        var result = new ObjectResult(problem) { StatusCode = status };
        result.ContentTypes.Add("application/problem+json");
        return result;
    }
}
