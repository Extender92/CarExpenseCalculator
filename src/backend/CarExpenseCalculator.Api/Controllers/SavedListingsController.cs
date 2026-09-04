using System.ComponentModel.DataAnnotations;
using CarExpenseCalculator.Api.Contracts.SavedListings;
using CarExpenseCalculator.Api.Mapping;
using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Core.Vehicles;
using CarExpenseCalculator.Infrastructure.Persistence.SavedListings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CarExpenseCalculator.Api.Controllers;

[ApiController]
[Route("api/saved-listings")]
public sealed class SavedListingsController(ISavedListingStore store) : ControllerBase
{
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<SavedListingResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(SavedListingProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<SavedListingResponse>> Create(
        CreateSavedListingRequest request,
        CancellationToken cancellationToken)
    {
        if (!RegistrationNumber.TryParse(request.RegistrationNumber, out var registrationNumber))
        {
            return RegistrationNumberValidationProblem();
        }

        try
        {
            var input = SavedListingMapper.ToStoreInput(request.Listing);
            var savedListing = await store.CreateAsync(
                registrationNumber!,
                input,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { vehicleId = savedListing.VehicleId },
                SavedListingMapper.ToApi(savedListing));
        }
        catch (SavedListingRequestMappingException exception)
        {
            return MappingValidationProblem(exception);
        }
        catch (ListingUrlValidationException exception)
        {
            return ListingValidationProblem("listing.submittedUrl", exception.Code);
        }
        catch (ListingValidationException exception)
        {
            return StoreValidationProblem(exception);
        }
        catch (SavedListingRegistrationConflictException exception)
        {
            return SavedProblem(
                StatusCodes.Status409Conflict,
                "A saved vehicle already exists for this registration number.",
                "registrationNumberConflict",
                existingVehicleId: exception.ExistingVehicleId,
                actualRevision: exception.ActualRevision);
        }
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SavedListingSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SavedListingProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<SavedListingSummaryResponse>>> List(
        CancellationToken cancellationToken)
    {
        try
        {
            var listings = await store.ListAsync(cancellationToken);
            return Ok(listings.Select(SavedListingMapper.ToSummaryApi).ToArray());
        }
        catch (UnsupportedSavedListingVersionException exception)
        {
            return UnsupportedVersionProblem(exception);
        }
    }

    [HttpGet("{vehicleId:guid}")]
    [ProducesResponseType<SavedListingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SavedListingProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(SavedListingProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<SavedListingResponse>> GetById(
        Guid vehicleId,
        CancellationToken cancellationToken)
    {
        try
        {
            var savedListing = await store.GetAsync(vehicleId, cancellationToken);
            return savedListing is null
                ? SavedListingNotFoundProblem()
                : Ok(SavedListingMapper.ToApi(savedListing));
        }
        catch (UnsupportedSavedListingVersionException exception)
        {
            return UnsupportedVersionProblem(exception);
        }
    }

    [HttpGet("by-registration/{registrationNumber}")]
    [ProducesResponseType<SavedListingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(SavedListingProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(SavedListingProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<SavedListingResponse>> GetByRegistrationNumber(
        string registrationNumber,
        CancellationToken cancellationToken)
    {
        if (!RegistrationNumber.TryParse(registrationNumber, out var normalizedRegistrationNumber))
        {
            return RegistrationNumberValidationProblem();
        }

        try
        {
            var savedListing = await store.GetByRegistrationNumberAsync(
                normalizedRegistrationNumber!,
                cancellationToken);
            return savedListing is null
                ? SavedListingNotFoundProblem()
                : Ok(SavedListingMapper.ToApi(savedListing));
        }
        catch (UnsupportedSavedListingVersionException exception)
        {
            return UnsupportedVersionProblem(exception);
        }
    }

    [HttpPut("{vehicleId:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType<SavedListingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(SavedListingProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(SavedListingProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<SavedListingResponse>> Replace(
        Guid vehicleId,
        ReplaceSavedListingRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var input = SavedListingMapper.ToStoreInput(request.Listing);
            var savedListing = await store.ReplaceAsync(
                vehicleId,
                request.ExpectedRevision,
                input,
                cancellationToken);
            return Ok(SavedListingMapper.ToApi(savedListing));
        }
        catch (SavedListingRequestMappingException exception)
        {
            return MappingValidationProblem(exception);
        }
        catch (ListingUrlValidationException exception)
        {
            return ListingValidationProblem("listing.submittedUrl", exception.Code);
        }
        catch (ListingValidationException exception)
        {
            return StoreValidationProblem(exception);
        }
        catch (SavedListingNotFoundException)
        {
            return SavedListingNotFoundProblem();
        }
        catch (SavedListingConcurrencyException exception)
        {
            return RevisionConflictProblem(exception);
        }
        catch (UnsupportedSavedListingVersionException exception)
        {
            return UnsupportedVersionProblem(exception);
        }
    }

    [HttpDelete("{vehicleId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(SavedListingProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(SavedListingProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Delete(
        Guid vehicleId,
        [FromQuery, BindRequired, Range(typeof(long), "1", "9223372036854775807")]
        long expectedRevision,
        CancellationToken cancellationToken)
    {
        try
        {
            await store.DeleteAsync(vehicleId, expectedRevision, cancellationToken);
            return NoContent();
        }
        catch (SavedListingNotFoundException)
        {
            return SavedListingNotFoundProblem();
        }
        catch (SavedListingConcurrencyException exception)
        {
            return RevisionConflictProblem(exception);
        }
    }

    private ActionResult RegistrationNumberValidationProblem()
    {
        return ValidationProblem(
            new ValidationProblemDetails(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["registrationNumber"] =
                    ["Registration number must be a supported ordinary Swedish registration number."],
                }));
    }

    private ActionResult MappingValidationProblem(SavedListingRequestMappingException exception)
    {
        return ValidationProblem(CreateValidationErrors(
            exception.Errors.Select(error => new ListingValidationError(
                $"listing.{error.Path}",
                error.Message))));
    }

    private ActionResult StoreValidationProblem(ListingValidationException exception)
    {
        return ValidationProblem(CreateValidationErrors(
            exception.Errors.Select(error => new ListingValidationError(
                PrefixStorePath(error.Path),
                error.Message))));
    }

    private ActionResult ListingValidationProblem(
        string path,
        ListingUrlValidationErrorCode code)
    {
        return ValidationProblem(CreateValidationErrors(
        [
            new ListingValidationError(path, ListingUrlValidationMessages.Get(code)),
        ]));
    }

    private ActionResult ValidationProblem(
        IDictionary<string, string[]> errors)
    {
        return ValidationProblem(new ValidationProblemDetails(errors));
    }

    private static Dictionary<string, string[]> CreateValidationErrors(
        IEnumerable<ListingValidationError> errors)
    {
        return errors
            .GroupBy(error => error.Path, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Message).ToArray(),
                StringComparer.Ordinal);
    }

    private static string PrefixStorePath(string path)
    {
        return path is "requestedModel" or "promptVersion" or "schemaVersion"
            || path.StartsWith("sources[", StringComparison.Ordinal)
            ? $"listing.{path}"
            : $"listing.draft.{path}";
    }

    private ObjectResult SavedListingNotFoundProblem()
    {
        return SavedProblem(
            StatusCodes.Status404NotFound,
            "The saved listing was not found.",
            "savedListingNotFound");
    }

    private ObjectResult RevisionConflictProblem(SavedListingConcurrencyException exception)
    {
        return SavedProblem(
            StatusCodes.Status409Conflict,
            "The saved vehicle has changed since it was loaded.",
            "revisionConflict",
            expectedRevision: exception.ExpectedRevision,
            actualRevision: exception.ActualRevision);
    }

    private ObjectResult UnsupportedVersionProblem(
        UnsupportedSavedListingVersionException exception)
    {
        return SavedProblem(
            StatusCodes.Status409Conflict,
            "The saved listing uses an unsupported version.",
            "unsupportedSavedListingVersion",
            listingSchemaVersion: exception.ListingSchemaVersion,
            promptVersion: exception.PromptVersion,
            schemaVersion: exception.ExtractionSchemaVersion);
    }

    private ObjectResult SavedProblem(
        int statusCode,
        string title,
        string code,
        Guid? existingVehicleId = null,
        long? expectedRevision = null,
        long? actualRevision = null,
        int? listingSchemaVersion = null,
        int? promptVersion = null,
        int? schemaVersion = null)
    {
        var problem = new SavedListingProblemDetails
        {
            Type = "about:blank",
            Title = title,
            Status = statusCode,
            Instance = HttpContext.Request.Path,
            Code = code,
            ExistingVehicleId = existingVehicleId,
            ExpectedRevision = expectedRevision,
            ActualRevision = actualRevision,
            ListingSchemaVersion = listingSchemaVersion,
            PromptVersion = promptVersion,
            SchemaVersion = schemaVersion,
        };

        return new ObjectResult(problem)
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" },
        };
    }
}
