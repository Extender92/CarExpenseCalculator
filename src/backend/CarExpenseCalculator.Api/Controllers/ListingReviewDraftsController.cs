using System.ComponentModel.DataAnnotations;
using CarExpenseCalculator.Api.Contracts.Households;
using CarExpenseCalculator.Api.Contracts.ListingReviewDrafts;
using CarExpenseCalculator.Api.Contracts.SavedListings;
using CarExpenseCalculator.Api.Mapping;
using CarExpenseCalculator.Infrastructure.Persistence.Households;
using CarExpenseCalculator.Infrastructure.Persistence.ListingReviewDrafts;
using Microsoft.AspNetCore.Mvc;

namespace CarExpenseCalculator.Api.Controllers;

[ApiController]
[Route("api/listing-review-drafts")]
[ProducesResponseType(typeof(HouseholdValidationProblemDetails), 400, "application/problem+json")]
[ProducesResponseType(typeof(HouseholdProblemDetails), 404, "application/problem+json")]
[ProducesResponseType(typeof(HouseholdProblemDetails), 409, "application/problem+json")]
[ProducesResponseType(typeof(HouseholdProblemDetails), 413, "application/problem+json")]
[ProducesResponseType(typeof(HouseholdProblemDetails), 503, "application/problem+json")]
public sealed class ListingReviewDraftsController(IListingReviewDraftStore store) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ListingReviewDraftResponse>>(200)]
    public async Task<ActionResult<IReadOnlyList<ListingReviewDraftResponse>>> List(CancellationToken ct) =>
        Ok((await store.ListAsync(ct)).Select(ToApi).ToArray());

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ListingReviewDraftResponse>(200)]
    public async Task<ActionResult<ListingReviewDraftResponse>> Get(Guid id, CancellationToken ct) =>
        Ok(ToApi(await store.GetAsync(id, ct)
            ?? throw new HouseholdStoreException("reviewDraftNotFound", "The listing review draft no longer exists.", reviewDraftId: id)));

    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<ListingReviewDraftResponse>(201)]
    public async Task<ActionResult<ListingReviewDraftResponse>> Create(CreateListingReviewDraftRequest request, CancellationToken ct)
    {
        var result = await store.CreateAsync(SavedListingMapper.ToStoreInput(request.Input), ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, ToApi(result));
    }

    [HttpPut("{id:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType<ListingReviewDraftResponse>(200)]
    public async Task<ActionResult<ListingReviewDraftResponse>> Replace(Guid id, ReplaceListingReviewDraftRequest request, CancellationToken ct)
    {
        HouseholdStoreMapper.Revision(request.ExpectedRevision, "expectedRevision");
        return Ok(ToApi(await store.ReplaceAsync(id, request.ExpectedRevision, SavedListingMapper.ToStoreInput(request.Input), ct)));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Delete(Guid id,
        [FromQuery, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired, Range(typeof(long), "1", "9223372036854775807")] long expectedRevision, CancellationToken ct)
    {
        await store.DeleteAsync(id, expectedRevision, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/adopt")]
    [Consumes("application/json")]
    [ProducesResponseType<SavedListingResponse>(200)]
    public async Task<ActionResult<SavedListingResponse>> Adopt(Guid id, AdoptListingReviewDraftRequest request, CancellationToken ct)
    {
        HouseholdStoreMapper.Revision(request.ExpectedRevision, "expectedRevision");
        if (request.ExpectedVehicleRevision is { } revision) HouseholdStoreMapper.Revision(revision, "expectedVehicleRevision");
        return Ok(SavedListingMapper.ToApi(await store.AdoptAsync(id, request.ExpectedRevision,
            request.ExistingVehicleId, request.ExpectedVehicleRevision, ct)));
    }

    private static ListingReviewDraftResponse ToApi(ListingReviewDraft draft) =>
        new(draft.Id, draft.Revision, draft.SchemaVersion, draft.ListingReference, draft.CreatedAtUtc, draft.UpdatedAtUtc,
            HouseholdDraftListingMapper.ToApi(draft.Input));
}
