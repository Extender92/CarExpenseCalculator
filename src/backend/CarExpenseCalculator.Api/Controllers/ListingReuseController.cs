using CarExpenseCalculator.Api.Contracts.Households;
using CarExpenseCalculator.Api.Contracts.ListingReviewDrafts;
using CarExpenseCalculator.Api.Mapping;
using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Infrastructure.Persistence.Comparisons;
using CarExpenseCalculator.Infrastructure.Persistence.Households;
using CarExpenseCalculator.Infrastructure.Persistence.ListingReviewDrafts;
using CarExpenseCalculator.Infrastructure.Persistence.SavedListings;
using Microsoft.AspNetCore.Mvc;
using C = CarExpenseCalculator.Core.Listings;
using A = CarExpenseCalculator.Api.Contracts.ListingReviewDrafts;

namespace CarExpenseCalculator.Api.Controllers;

[ApiController]
[Route("api/listing-reuse")]
[ProducesResponseType(typeof(HouseholdValidationProblemDetails), 400, "application/problem+json")]
[ProducesResponseType(typeof(HouseholdProblemDetails), 404, "application/problem+json")]
[ProducesResponseType(typeof(HouseholdProblemDetails), 409, "application/problem+json")]
[ProducesResponseType(typeof(HouseholdProblemDetails), 413, "application/problem+json")]
[ProducesResponseType(typeof(HouseholdProblemDetails), 503, "application/problem+json")]
public sealed class ListingReuseController(IComparisonSnapshotStore snapshots, IListingReviewDraftStore drafts,
    ISavedListingStore listings, TimeProvider clock) : ControllerBase
{
    [HttpPost("preview")]
    [Consumes("application/json")]
    [ProducesResponseType<ListingReusePreviewResponse>(200)]
    public async Task<ActionResult<ListingReusePreviewResponse>> Preview(ListingReusePreviewRequest request, CancellationToken ct)
    {
        var count = (request.VehicleId.HasValue ? 1 : 0) + (request.ReviewDraftId.HasValue ? 1 : 0) + (request.UnsavedListing is not null ? 1 : 0);
        if (count != 1 || request.VehicleId.HasValue != request.ExpectedVehicleRevision.HasValue ||
            request.VehicleId.HasValue != request.ExpectedListingVersion.HasValue ||
            request.ReviewDraftId.HasValue != request.ExpectedReviewDraftRevision.HasValue)
            throw HouseholdInputMapper.Error("source", "invalidListingSource", "Choose exactly one source with its applicable revisions.");
        SavedListingInput input;
        long? listingVersion = null;
        SavedVehicleFacts? current = null;
        if (request.VehicleId is { } id)
        {
            HouseholdStoreMapper.Revision(request.ExpectedVehicleRevision!.Value, "expectedVehicleRevision");
            HouseholdStoreMapper.Revision(request.ExpectedListingVersion!.Value, "expectedListingVersion");
            var snapshot = (await snapshots.ReadAsync([id], ct)).Vehicles.Single();
            if (snapshot.Cost.Revision != request.ExpectedVehicleRevision)
                throw new HouseholdStoreException("vehicleRevisionConflict", "The vehicle changed before reuse.", id,
                    request.ExpectedVehicleRevision, snapshot.Cost.Revision);
            var saved = snapshot.Listing ?? throw new HouseholdStoreException("listingRequired", "No saved listing is available.", id);
            if (saved.ListingVersion != request.ExpectedListingVersion)
                throw new HouseholdStoreException("listingVersionConflict", "The source listing changed before reuse.", id,
                    request.ExpectedListingVersion, saved.ListingVersion);
            input = new(saved.SubmittedUrl, saved.AnalyzedAtUtc, saved.RequestedModel, saved.PromptVersion,
                saved.ExtractionSchemaVersion, saved.ProcessingResult.Sources.Select(x => x.Url), saved.ProcessingResult.Listing);
            listingVersion = saved.ListingVersion;
            current = snapshot.Facts;
        }
        else if (request.ReviewDraftId is { } draftId)
        {
            HouseholdStoreMapper.Revision(request.ExpectedReviewDraftRevision!.Value, "expectedReviewDraftRevision");
            var draft = await drafts.GetAsync(draftId, ct)
                ?? throw new HouseholdStoreException("reviewDraftNotFound", "The review draft no longer exists.", reviewDraftId: draftId);
            if (draft.Revision != request.ExpectedReviewDraftRevision)
                throw new HouseholdStoreException("reviewDraftRevisionConflict", "The review draft changed before reuse.",
                    expectedRevision: request.ExpectedReviewDraftRevision, actualRevision: draft.Revision, reviewDraftId: draftId);
            input = draft.Input;
        }
        else input = listings.NormalizeReviewDraft(SavedListingMapper.ToStoreInput(request.UnsavedListing!));
        var target = HouseholdInputMapper.ToCore(request.Target, "target");
        var errors = CarExpenseCalculator.Core.Households.HouseholdCostInputValidator.ValidateVehicle(target, "target");
        if (errors.Count > 0) throw new CarExpenseCalculator.Core.Households.HouseholdInputValidationException(errors);
        var facts = request.FactEdits is null ? current?.Input
            : ComparisonFactOperations.Normalize(ComparisonFactOperations.Apply(current?.Input,
                ComparisonInputMapper.ToStore(request.FactEdits), current?.ListingProposal, listingVersion,
                ComparisonFactOperations.OperationTime(clock)), target.AcquisitionType);
        var preview = C.ListingReuse.Preview(ListingUrl.Parse(input.SubmittedUrl), input.Sources, input.Listing,
            listingVersion, target, facts?.Facts);
        var versions = preview.FactTargets.ToDictionary(x => x.Field, _ => (IReadOnlyList<long?>)new long?[] { listingVersion });
        for (var i = 0; i < (preview.ConditionNotes?.Count ?? 0); i++) versions[$"conditionNotes[{i}]"] = [listingVersion];
        return Ok(new ListingReusePreviewResponse(
            preview.PurchasePrice is not { } price ? null : Map(price, price.Value),
            preview.EnergySources.Select(x => Map(x, HouseholdInputMapper.ToApi(x.Value))).ToArray(),
            preview.AnnualTax is not { } tax ? null : Map(tax, HouseholdInputMapper.ToApi(tax.Value)),
            ComparisonInputMapper.ToApi(new ComparisonFactSet(preview.Facts, versions, preview.ConditionNotes)),
            preview.FactTargets.Select(x => new A.ListingFactReuseTarget(x.Field, x.RequiresReplacement, x.AlreadyApplied)).ToArray(),
            preview.Warnings));
    }

    private static A.ListingReuseSuggestion<TOut> Map<TIn, TOut>(C.ListingReuseSuggestion<TIn> input, TOut value) where TIn : notnull where TOut : notnull =>
        new(input.Key, value, HouseholdInputMapper.ToApi(input.Source)!, input.RequiresReplacement, input.AlreadyApplied);
}
