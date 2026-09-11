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
        var prepared = await PrepareAsync(request, services, ct);
        return Present(prepared, new C.ComparisonEvaluator().EvaluateComparison(prepared.Profile, prepared.Rules,
            request.AsOfDate, prepared.Candidates), ct);
    }

    public async Task<CompleteComparisonResponse> PreviewAllAsync(CompleteComparisonRequest request, IServiceProvider services, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!Enum.IsDefined(request.Mode)) throw Error("mode", "invalidEnum", "Unsupported comparison mode.");
        var stored = request.Mode == ComparisonPreviewMode.Stored;
        if (stored ? request.StoredBase is null || request.Candidates is not null
            : request.StoredBase is not null || request.Overrides is not null || request.Candidates is null)
            throw Error("mode", "invalidMode", "Stored mode uses a baseline and overrides; manual mode uses candidates only.");
        var candidates = stored ? request.Overrides ?? [] : request.Candidates!;
        var effectiveRequest = new ComparisonPreviewRequest
        {
            Mode = request.Mode, RequestId = request.RequestId, Profile = request.Profile, Rules = request.Rules,
            AsOfDate = request.AsOfDate, Candidates = candidates,
            StoredBase = request.StoredBase is not { } b ? null : new()
            { HouseholdProfileRevision = b.HouseholdProfileRevision, RuleProfileRevision = b.RuleProfileRevision },
        };
        try { ValidateEnvelope(effectiveRequest, true); }
        catch (C.ComparisonInputValidationException e) when (stored)
        { throw OverrideErrors(e, i => i); }
        S.ComparisonSnapshot? snapshot = null;
        string? baselineToken = null;
        if (stored)
        {
            var token = request.StoredBase!.BaselineToken;
            if (token is null || token.Length != 67 || !token.StartsWith("v1:", StringComparison.Ordinal) ||
                token.AsSpan(3).ContainsAnyExcept("0123456789abcdef"))
                throw Error("storedBase.baselineToken", "invalidBaselineToken", "A version-1 baseline token is required.");
            var complete = await services.GetRequiredService<S.IComparisonSnapshotStore>().ReadAllAsync(token, ct);
            snapshot = complete.Snapshot;
            if (snapshot.Vehicles.Count != complete.Baseline.CandidateCount)
                throw new InvalidDataException("The complete comparison snapshot is missing a group.");
            baselineToken = complete.Baseline.BaselineToken;
            var overrides = candidates.ToDictionary(x => x.VehicleId);
            var ids = snapshot.Vehicles.Select(x => x.Facts.VehicleId).ToHashSet();
            foreach (var x in candidates)
                if (!ids.Contains(x.VehicleId)) throw new S.ComparisonStoreException("vehicleNotFound", "Vehicle no longer exists.", x.VehicleId);
            effectiveRequest = effectiveRequest with
            {
                Candidates = snapshot.Vehicles.Select(x => overrides.TryGetValue(x.Facts.VehicleId, out var edit) ? edit : new ComparisonCandidateRequest
                {
                    VehicleId = x.Facts.VehicleId, RegistrationNumber = x.Facts.RegistrationNumber.Value,
                    StoredBase = new() { VehicleRevision = x.Facts.Revision, Listing = new() { Version = x.Facts.CurrentListingVersion } },
                }).ToArray(),
            };
        }
        PreparedComparison prepared;
        try { prepared = await PrepareAsync(effectiveRequest, services, ct, snapshot, true); }
        catch (C.ComparisonInputValidationException e) when (stored)
        {
            var overridePositions = candidates.Select((x, i) => (x.VehicleId, i)).ToDictionary(x => x.VehicleId, x => x.i);
            throw OverrideErrors(e, i => i < effectiveRequest.Candidates.Count &&
                overridePositions.TryGetValue(effectiveRequest.Candidates[i].VehicleId, out var position) ? position : null);
        }
        var evaluator = new C.ComparisonEvaluator();
        ComparisonPreviewResponse View(H.SensitivityMode mode)
        {
            ct.ThrowIfCancellationRequested();
            return Present(prepared, evaluator.EvaluateAllComparison(prepared.Profile with { ActiveSensitivityMode = mode },
                prepared.Rules, request.AsOfDate, prepared.Candidates, ct), ct);
        }
        var views = new CompleteComparisonViews(View(H.SensitivityMode.Baseline), View(H.SensitivityMode.Favorable), View(H.SensitivityMode.Cautious));
        ct.ThrowIfCancellationRequested();
        return new(request.RequestId, Guid.NewGuid(), request.Mode, baselineToken, prepared.Candidates.Count,
            request.Profile.ActiveSensitivityMode, views)
        {
            Listings = snapshot?.Vehicles.Where(x => x.Listing is not null)
                .Select(x => SavedListingMapper.ToApi(x.Listing!)).ToArray() ?? [],
        };
    }

    private static C.ComparisonInputValidationException OverrideErrors(C.ComparisonInputValidationException error, Func<int, int?> position)
        => new(error.Errors.Select(x =>
        {
            if (!x.Path.StartsWith("candidates[", StringComparison.Ordinal)) return x;
            var end = x.Path.IndexOf(']');
            return end > 11 && int.TryParse(x.Path.AsSpan(11, end - 11), out var index) && position(index) is { } target
                ? new C.ComparisonInputError($"overrides[{target}]" + x.Path[(end + 1)..], x.Code, x.Message) : x;
        }));

    private static void ValidateEnvelope(ComparisonPreviewRequest request, bool all)
    {
        if (!Enum.IsDefined(request.Mode)) throw Error("mode", "invalidEnum", "Unsupported comparison mode.");
        if (string.IsNullOrWhiteSpace(request.RequestId) || request.RequestId.Length > 120)
            throw Error("requestId", "invalidRequestId", "Request ID must contain 1-120 characters.");
        if (!all && request.Candidates.Count > 100) throw Error("candidates", "tooManyItems", "At most 100 candidates may be compared.");
        var stored = request.Mode == ComparisonPreviewMode.Stored;
        if (stored != (request.StoredBase is not null)) throw Error("storedBase", "invalidMode", "Stored mode requires shared base revisions; manual mode forbids them.");
        var ids = new HashSet<Guid>(); var registrations = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < request.Candidates.Count; i++)
        {
            var x = request.Candidates[i] ?? throw Error($"candidates[{i}]", "required", "A candidate is required.");
            var reg = HouseholdStoreMapper.Registration(x.RegistrationNumber, $"candidates[{i}].registrationNumber");
            if (x.VehicleId == Guid.Empty || !ids.Add(x.VehicleId)) throw Error($"candidates[{i}].vehicleId", "duplicateIdentity", "Each candidate needs a unique nonempty UUID.");
            if (!registrations.Add(reg.Value)) throw Error($"candidates[{i}].registrationNumber", "duplicateRegistration", "Each registration may occur once.");
            if (stored != (x.StoredBase is not null)) throw Error($"candidates[{i}].storedBase", "invalidMode", "Candidate base revisions must match the selected mode.");
            if (!stored && (x.Facts.ExpectedListingVersion is not null || x.Facts.ReviewCurrentListing || x.LegacyDecisions is not null))
                throw Error($"candidates[{i}]", "invalidMode", "Manual mode cannot assert saved listing or review state.");
        }
        _ = HouseholdInputMapper.ToCore(request.Profile, "profile");
        _ = ComparisonInputMapper.NormalizeRules(request.Rules, "rules");
    }

    private async Task<PreparedComparison> PrepareAsync(ComparisonPreviewRequest request, IServiceProvider services, CancellationToken ct,
        S.ComparisonSnapshot? suppliedSnapshot = null, bool all = false)
    {
        ct.ThrowIfCancellationRequested();
        ValidateEnvelope(request, all);
        var stored = request.Mode == ComparisonPreviewMode.Stored;
        var normalizedRegistrations = request.Candidates.Select((x, i) =>
            HouseholdStoreMapper.Registration(x.RegistrationNumber, $"candidates[{i}].registrationNumber")).ToArray();
        var profile = HouseholdInputMapper.ToCore(request.Profile, "profile")!;
        var rules = ComparisonInputMapper.NormalizeRules(request.Rules, "rules");
        S.ComparisonSnapshot? snapshot = suppliedSnapshot;
        if (stored)
        {
            snapshot ??= await services.GetRequiredService<S.IComparisonSnapshotStore>()
                .ReadAsync(request.Candidates.Select(x => x.VehicleId).ToArray(), ct);
            S.VehicleFactsStore.Revision(request.StoredBase!.HouseholdProfileRevision, snapshot.Profile.Revision, "profileRevisionConflict");
            S.VehicleFactsStore.Revision(request.StoredBase.RuleProfileRevision, snapshot.Rules.Revision, "ruleProfileRevisionConflict");
        }
        var now = S.ComparisonFactOperations.OperationTime(timeProvider);
        var profileDirty = snapshot is null || !ComparisonInputMapper.Equivalent(HouseholdInputMapper.ToApi(snapshot.Profile.Input), request.Profile);
        var rulesDirty = snapshot?.Rules.Input is null || !ComparisonInputMapper.Equivalent(ComparisonInputMapper.ToApi(snapshot.Rules.Input), ComparisonInputMapper.ToApi(rules));
        var effective = new List<C.ComparisonCandidateInput>();
        var contexts = new List<CandidateContext>();
        for (var i = 0; i < request.Candidates.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
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
                    profileDirty, rulesDirty,
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
        return new(request, profile, rules, effective.AsReadOnly(), contexts.AsReadOnly());
    }

    private static ComparisonPreviewResponse Present(PreparedComparison prepared, C.ComparisonPreview result, CancellationToken ct)
    {
        var request = prepared.Request;
        var contexts = prepared.Contexts;
        var candidates = result.Candidates.Select((v, i) =>
        {
            ct.ThrowIfCancellationRequested();
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
        return new(request.RequestId, request.Mode, request.Mode == ComparisonPreviewMode.Stored, HouseholdInputMapper.ToApi(result.Profile), ComparisonInputMapper.ToApi(result.Rules),
            result.AsOfDate, result.RuleVersion, result.ResultSchemaVersion, result.CalculationVersion, result.HouseholdResultSchemaVersion,
            result.ProfileErrors.Select(x => new ComparisonFieldError(x.Path, x.Code, x.Message)).ToArray(),
            candidates, result.CostOrder, result.ScoreOrder, result.PreferenceRecommendationReason);
    }

    private sealed record PreparedComparison(ComparisonPreviewRequest Request, H.HouseholdProfileInput Profile,
        C.RuleProfileInput Rules, IReadOnlyList<C.ComparisonCandidateInput> Candidates, IReadOnlyList<CandidateContext> Contexts);

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
