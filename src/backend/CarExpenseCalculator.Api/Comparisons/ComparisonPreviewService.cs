using CarExpenseCalculator.Api.Contracts.Comparisons;
using CarExpenseCalculator.Api.Mapping;
using CarExpenseCalculator.Core.Vehicles;
using C = CarExpenseCalculator.Core.Comparisons;
using H = CarExpenseCalculator.Core.Households;
using AH = CarExpenseCalculator.Api.Contracts.Households;
using S = CarExpenseCalculator.Infrastructure.Persistence.Comparisons;
using HS = CarExpenseCalculator.Infrastructure.Persistence.Households;

namespace CarExpenseCalculator.Api.Comparisons;

internal sealed class ComparisonPreviewService(TimeProvider timeProvider)
{
    public async Task<ComparisonPreviewResponse> PreviewAsync(ComparisonPreviewRequest request, IServiceProvider services, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!Enum.IsDefined(request.Mode)) throw Error("mode", "invalidEnum", "Unsupported comparison mode.");
        if (string.IsNullOrWhiteSpace(request.RequestId) || request.RequestId.Length > 120)
            throw Error("requestId", "invalidRequestId", "Request ID must contain 1-120 characters.");
        if (request.Candidates.Count > 100) throw Error("candidates", "tooManyItems", "At most 100 candidates may be compared.");
        var stored = request.Mode == ComparisonPreviewMode.Stored;
        if (stored != (request.StoredBase is not null)) throw Error("storedBase", "invalidMode", "Stored mode requires shared base revisions; manual mode forbids them.");
        var ids = new HashSet<Guid>(); var registrations = new HashSet<string>(StringComparer.Ordinal);
        var normalizedRegistrations = new List<RegistrationNumber>();
        for (var i = 0; i < request.Candidates.Count; i++)
        {
            var x = request.Candidates[i] ?? throw Error($"candidates[{i}]", "required", "A candidate is required.");
            var reg = HouseholdStoreMapper.Registration(x.RegistrationNumber, $"candidates[{i}].registrationNumber");
            if (x.VehicleId == Guid.Empty || !ids.Add(x.VehicleId)) throw Error($"candidates[{i}].vehicleId", "duplicateIdentity", "Each candidate needs a unique nonempty UUID.");
            if (!registrations.Add(reg.Value)) throw Error($"candidates[{i}].registrationNumber", "duplicateRegistration", "Each registration may occur once.");
            if (stored != (x.StoredBase is not null)) throw Error($"candidates[{i}].storedBase", "invalidMode", "Candidate base revisions must match the selected mode.");
            if (!stored && (x.Facts.ExpectedListingVersion is not null || x.Facts.ReviewCurrentListing || x.LegacyDecisions is not null))
                throw Error($"candidates[{i}]", "invalidMode", "Manual mode cannot assert saved listing or review state.");
            normalizedRegistrations.Add(reg);
        }
        var profile = HouseholdInputMapper.ToCore(request.Profile, "profile")!;
        var rules = ComparisonInputMapper.NormalizeRules(request.Rules, "rules");
        S.ComparisonSnapshot? snapshot = null;
        if (stored)
        {
            snapshot = await services.GetRequiredService<S.IComparisonSnapshotStore>()
                .ReadAsync(request.Candidates.Select(x => x.VehicleId).ToArray(), ct);
            S.VehicleFactsStore.Revision(request.StoredBase!.HouseholdProfileRevision, snapshot.Profile.Revision, "profileRevisionConflict");
            S.VehicleFactsStore.Revision(request.StoredBase.RuleProfileRevision, snapshot.Rules.Revision, "ruleProfileRevisionConflict");
        }
        var now = S.ComparisonFactOperations.OperationTime(timeProvider);
        var effective = new List<C.ComparisonCandidateInput>();
        var contexts = new List<CandidateContext>();
        for (var i = 0; i < request.Candidates.Count; i++)
        {
            var x = request.Candidates[i];
            var saved = snapshot?.Vehicles[i];
            if (saved is not null)
            {
                S.VehicleFactsStore.Revision(x.StoredBase!.VehicleRevision, saved.Facts.Revision, "vehicleRevisionConflict", x.VehicleId);
                if (normalizedRegistrations[i] != saved.Facts.RegistrationNumber)
                    throw new S.ComparisonStoreException("comparisonIdentityMismatch", "UUID and registration do not identify the same current vehicle.", x.VehicleId);
                if (x.StoredBase.Listing.Version != saved.Facts.CurrentListingVersion)
                    throw new S.ComparisonStoreException("listingVersionConflict", "Listing version has changed.", x.VehicleId,
                        x.StoredBase.Listing.Version, saved.Facts.CurrentListingVersion);
            }
            try
            {
                var write = ComparisonInputMapper.ToStore(x.Facts);
                if (saved is not null) S.VehicleFactsStore.CheckListing(write, saved.Facts);
                var facts = S.ComparisonFactOperations.Apply(saved?.Facts.Input, write.Edits, saved?.Facts.ListingProposal,
                    saved?.Facts.CurrentListingVersion, now);
                var cost = x.CostInput is null ? saved?.Cost.Input : HouseholdInputMapper.ToCore(x.CostInput, $"candidates[{i}].costInput");
                if (cost is not null) cost = cost with { CandidateKey = normalizedRegistrations[i].Value };
                // Only numeric domain errors belong in independent 200 results. Invalid actions,
                // text, collections, conflicts and evidence remain request-structure errors.
                try { facts = S.ComparisonFactOperations.Normalize(facts, cost?.AcquisitionType ?? H.AcquisitionType.Purchase); }
                catch (C.VehicleFactsValidationException e)
                {
                    var structural = e.Errors.Where(v => v.Code != "outOfRange").ToArray();
                    if (structural.Length > 0) throw new C.ComparisonInputValidationException(
                        structural.Select(v => new C.ComparisonInputError(v.Path, v.Code, v.Message)));
                }
                var oldCost = saved?.Cost.Input;
                var sameCost = oldCost is null ? cost is null : C.CostAssumptionConfirmation.Confirm(oldCost, now).IsApplicableTo(cost);
                var oldConfirmation = sameCost ? saved?.Facts.CostConfirmedAt : null;
                var confirmedAt = S.VehicleFactsStore.Confirmation(write.CostConfirmation, oldConfirmation, cost, now);
                var decisions = x.LegacyDecisions?.Select(d => d is null ? throw Error("legacyDecisions", "required", "A decision is required.")
                    : new HS.LegacyItemDecision(d.Key, (HS.LegacyItemDisposition)d.Disposition, d.TargetKey)).ToArray();
                var originalReviews = saved?.Cost.Legacy?.Items ?? saved?.Cost.UnresolvedLegacyItems ?? [];
                var reviews = S.ComparisonFactOperations.ResolveReviews(originalReviews, cost, decisions);
                var reviewedListing = write.ReviewCurrentListing ? saved?.Facts.CurrentListingVersion : saved?.Facts.FactsReviewedListingVersion;
                var revisions = new ComparisonSourceRevisions(snapshot?.Profile.Revision, snapshot?.Rules.Revision, saved?.Facts.Revision,
                    saved?.Facts.CurrentListingVersion, reviewedListing, saved?.Facts.CostReviewedListingVersion);
                var dirty = new ComparisonDirtyState(
                    snapshot is null || !ComparisonInputMapper.Equivalent(HouseholdInputMapper.ToApi(snapshot.Profile.Input), request.Profile),
                    snapshot?.Rules.Input is null || !ComparisonInputMapper.Equivalent(ComparisonInputMapper.ToApi(snapshot.Rules.Input), ComparisonInputMapper.ToApi(rules)),
                    !ComparisonInputMapper.Equivalent(saved?.Facts.Input is null ? null : ComparisonInputMapper.ToApi(saved.Facts.Input), ComparisonInputMapper.ToApi(facts)),
                    !sameCost, !ComparisonInputMapper.Equivalent(originalReviews, reviews),
                    confirmedAt != saved?.Facts.CostConfirmedAt, reviewedListing != saved?.Facts.FactsReviewedListingVersion);
                effective.Add(new(x.VehicleId, normalizedRegistrations[i], facts.Facts, cost,
                    confirmedAt is null || cost is null ? null : C.CostAssumptionConfirmation.Confirm(cost, confirmedAt.Value),
                    new(revisions.Vehicle, revisions.Listing, revisions.FactsReviewedListing, revisions.HouseholdProfile, revisions.RuleProfile),
                    reviews.Select(S.ComparisonFactOperations.ToCoreReview), facts.ConditionNotes));
                contexts.Add(new(facts, confirmedAt, reviews, revisions,
                    saved?.Facts.CurrentListingVersion is not null && reviewedListing != saved.Facts.CurrentListingVersion,
                    saved is null ? null : (AH.VehicleInputState)saved.Cost.State, dirty));
            }
            catch (C.ComparisonInputValidationException e)
            { throw new C.ComparisonInputValidationException(e.Errors.Select(v => new C.ComparisonInputError(
                $"candidates[{i}]." + (v.Path.StartsWith("legacyDecisions", StringComparison.Ordinal)
                    ? v.Path : "facts." + ComparisonInputMapper.FactWritePath(v.Path)), v.Code, v.Message))); }
        }
        var result = new C.ComparisonEvaluator().EvaluateComparison(profile, rules, request.AsOfDate, effective);
        var candidates = result.Candidates.Select((v, i) =>
        {
            var context = contexts[i];
            var normalized = new S.ComparisonFactSet(v.EffectiveInput.Facts, context.Facts.ObservationListingVersions, v.EffectiveInput.ConditionNotes);
            return new ComparisonCandidateResult(v.EffectiveInput.VehicleId, v.EffectiveInput.RegistrationNumber.Value,
                ComparisonInputMapper.ToApi(normalized), HouseholdInputMapper.ToApi(v.EffectiveInput.CostInput), v.EffectiveInput.CostConfirmation?.ConfirmedAt,
                context.Reviews.Select(HouseholdStoreMapper.ToApi).ToArray(), context.Revisions, context.NeedsListingReview,
                context.StoredInputState, context.Dirty, HouseholdResultMapper.ToApi(v.Cost), v.Errors.Select(FieldError).ToArray(),
                v.HardRules.Select(h => new HardRuleEvaluation(ComparisonInputMapper.ToApi(h.Rule), (HardRuleState)h.State,
                    Assessment(h.Assessment), h.ObservedConditionSatisfied, h.ReasonCode, h.Explanation)).ToArray(),
                (BuyingEligibility)v.Eligibility, v.Contributions.Select(p => new PreferenceContribution(ComparisonInputMapper.ToApi(p.Preference),
                    Assessment(p.Assessment), Range(p.Range)!, Range(p.WeightedContribution)!, p.Explanation)).ToArray(),
                Range(v.Score), v.CoveragePercent, v.ScoreUnavailableReason,
                v.Signals.Select(s => new ComparisonSignal((ComparisonSignalKey)s.Key, (ComparisonSignalKind)s.Kind,
                    s.ReasonCode, s.Explanation, s.Evidence is null ? null : ComparisonInputMapper.ToApi(s.Evidence))).ToArray(),
                v.IsCheapestEligibleComplete, v.IsDefinitePreferenceWinner);
        }).ToArray();
        ct.ThrowIfCancellationRequested();
        return new(request.RequestId, request.Mode, stored, HouseholdInputMapper.ToApi(result.Profile), ComparisonInputMapper.ToApi(result.Rules),
            result.AsOfDate, result.RuleVersion, result.ResultSchemaVersion, result.CalculationVersion, result.HouseholdResultSchemaVersion,
            result.ProfileErrors.Select(x => new ComparisonFieldError(x.Path, x.Code, x.Message)).ToArray(),
            candidates, result.CostOrder, result.ScoreOrder, result.PreferenceRecommendationReason);
    }

    private sealed record CandidateContext(S.ComparisonFactSet Facts, DateTimeOffset? ConfirmedAt, IReadOnlyList<HS.LegacyReviewItem> Reviews,
        ComparisonSourceRevisions Revisions, bool NeedsListingReview, AH.VehicleInputState? StoredInputState, ComparisonDirtyState Dirty);
    private static ComparisonFieldError FieldError(C.ComparisonInputError x) => new(x.Path, x.Code, x.Message);
    private static ScoreRange? Range(C.ScoreRange? x) => x is null ? null : new(x.Lower, x.Upper);
    private static CriterionAssessment Assessment(C.CriterionAssessment x) => new(x.CriterionKey, x.Actual is not { } actual ? null :
        new(actual.Number, actual.Choice is null ? null : ComparisonInputMapper.ToApi(actual.Choice), actual.Date,
            actual.Fuels?.Select(v => (Contracts.ListingAnalyses.FuelType)v).ToArray()),
        x.Evidence.Select(ComparisonInputMapper.ToApi).ToArray(), (EvidenceRequirement)x.MinimumEvidence, x.HasAdequateEvidence,
        x.Reasons, x.Errors.Select(FieldError).ToArray());
    private static C.ComparisonInputValidationException Error(string path, string code, string message) => S.ComparisonFactOperations.Error(path, code, message);
}
