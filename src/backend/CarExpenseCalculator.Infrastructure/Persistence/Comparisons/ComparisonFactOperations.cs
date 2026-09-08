using CarExpenseCalculator.Core.Comparisons;
using CarExpenseCalculator.Core.Households;
using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Infrastructure.Persistence.Households;

namespace CarExpenseCalculator.Infrastructure.Persistence.Comparisons;

// Shared application boundary for persisted writes and unsaved previews. No client evidence is accepted.
public static class ComparisonFactOperations
{
    public static DateTimeOffset OperationTime(TimeProvider clock)
    {
        var now = clock.GetUtcNow();
        // PostgreSQL timestamptz stores microseconds. Use the same instant in save responses and subsequent reads.
        return new DateTimeOffset(now.UtcTicks - now.UtcTicks % 10, TimeSpan.Zero);
    }

    public static ComparisonFactSet Apply(ComparisonFactSet? current, VehicleFactEdits edits,
        ComparisonFactSet? listing, long? listingVersion, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(edits);
        var prior = current?.Facts ?? new();
        var proposal = listing?.Facts ?? new();
        var versions = new Dictionary<string, IReadOnlyList<long?>>(StringComparer.Ordinal);
        VehicleFact<T> Field<T>(string key, VehicleFact<T>? old, FactEdit<T>? edit, VehicleFact<T>? source) where T : notnull
        {
            old ??= VehicleFact<T>.Unknown();
            var oldVersions = current?.ObservationListingVersions.GetValueOrDefault(key)
                ?? Array.AsReadOnly(new long?[old.Observations.Count]);
            if (edit is null || edit.Kind == FactEditKind.Preserve)
            {
                if (edit is not null && (edit.Manual is not null || edit.Observations is not null)) throw Error(key, "invalidFactOperation", "Preserve has no supplied values.");
                versions[key] = oldVersions;
                return old;
            }
            if (!Enum.IsDefined(edit.Kind)) throw Error(key, "invalidEnum", "Unknown fact operation.");
            if (edit.Kind != FactEditKind.Conflict && edit.Observations is not null)
                throw Error(key, "invalidFactOperation", "Only a conflict contains observation selections.");
            if (edit.Kind is not (FactEditKind.Manual or FactEditKind.Resolve) && edit.Manual is not null)
                throw Error(key, "invalidFactOperation", "This operation does not accept a manual value.");
            if (old.State == VehicleFactState.Conflicting && edit.Kind is FactEditKind.Manual or FactEditKind.Listing)
                throw Error(key, "conflictResolutionRequired", "Resolve or explicitly clear the existing conflict.");
            var selected = new List<(FactObservation<T> Observation, long? Version)>();
            (FactObservation<T>, long?) Selection(FactSelection<T> selection)
            {
                if (selection is null || !Enum.IsDefined(selection.Kind)) throw Error(key, "invalidFactOperation", "An observation selection is required.");
                if (selection.Kind == FactSelectionKind.Current)
                {
                    if (selection.Manual is not null || selection.ObservationIndex is not { } index || index < 0 || index >= old.Observations.Count)
                        throw Error(key, "invalidObservationReference", "The current observation reference is invalid.");
                    return (old.Observations[index], oldVersions[index]);
                }
                if (selection.ObservationIndex is not null) throw Error(key, "invalidObservationReference", "Only current observations use an index.");
                if (selection.Kind == FactSelectionKind.Listing)
                {
                    if (selection.Manual is not null || listingVersion is null || source?.State != VehicleFactState.Known)
                        throw Error(key, "listingFactUnavailable", "The current listing has no known equivalent fact.");
                    return (source.Observations[0], listingVersion);
                }
                if (selection.Manual is not { } manual) throw Error(key, "required", "A manual value is required.");
                return (old.ReplaceWithManual(manual.Value, now, manual.ObservedAt).Observations[0], null);
            }
            VehicleFact<T> result;
            switch (edit.Kind)
            {
                case FactEditKind.Unknown: result = VehicleFact<T>.Unknown(); break;
                case FactEditKind.NotApplicable: result = VehicleFact<T>.NotApplicable(); break;
                case FactEditKind.Manual:
                case FactEditKind.Resolve:
                    if (edit.Manual is not { } value) throw Error(key, "required", "A manual value is required.");
                    if (edit.Kind == FactEditKind.Resolve && old.State != VehicleFactState.Conflicting)
                        throw Error(key, "invalidState", "Only conflicting facts can be explicitly resolved.");
                    result = edit.Kind == FactEditKind.Resolve
                        ? old.ResolveWithManual(value.Value, now, value.ObservedAt)
                        : old.ReplaceWithManual(value.Value, now, value.ObservedAt);
                    selected.Add((result.Observations[0], null));
                    break;
                case FactEditKind.Listing:
                    selected.Add(Selection(new(FactSelectionKind.Listing)));
                    result = VehicleFact<T>.Known(selected[0].Observation.Value, selected[0].Observation.Evidence);
                    break;
                case FactEditKind.Conflict:
                    if (edit.Observations is not { Count: >= 2 }) throw Error(key, "invalidConflict", "A conflict needs at least two selected observations.");
                    selected.AddRange(edit.Observations.Select(Selection));
                    result = VehicleFact<T>.Conflicting(selected.Select(x => x.Observation));
                    break;
                default: throw Error(key, "invalidFactOperation", "Unsupported operation.");
            }
            versions[key] = Array.AsReadOnly(selected.Select(x => x.Version).ToArray());
            return result;
        }
        var facts = new VehicleComparisonFacts
        {
            PurchasePriceSek = Field("purchasePriceSek", prior.PurchasePriceSek, edits.PurchasePriceSek, proposal.PurchasePriceSek),
            OdometerKilometres = Field("odometerKilometres", prior.OdometerKilometres, edits.OdometerKilometres, proposal.OdometerKilometres),
            OwnerCount = Field("ownerCount", prior.OwnerCount, edits.OwnerCount, proposal.OwnerCount),
            TowBar = Field("towBar", prior.TowBar, edits.TowBar, proposal.TowBar),
            Transmission = Field("transmission", prior.Transmission, edits.Transmission, proposal.Transmission),
            Seats = Field("seats", prior.Seats, edits.Seats, proposal.Seats),
            ModelYear = Field("modelYear", prior.ModelYear, edits.ModelYear, proposal.ModelYear),
            FuelTypes = Field("fuelTypes", prior.FuelTypes, edits.FuelTypes, proposal.FuelTypes),
            BodyType = Field("bodyType", prior.BodyType, edits.BodyType, proposal.BodyType),
            Drivetrain = Field("drivetrain", prior.Drivetrain, edits.Drivetrain, proposal.Drivetrain),
            Locality = Field("locality", prior.Locality, edits.Locality, proposal.Locality),
            County = Field("county", prior.County, edits.County, proposal.County),
            TowingCapacityKilograms = Field("towingCapacityKilograms", prior.TowingCapacityKilograms, edits.TowingCapacityKilograms, proposal.TowingCapacityKilograms),
            InspectionValidThrough = Field("inspectionValidThrough", prior.InspectionValidThrough, edits.InspectionValidThrough, proposal.InspectionValidThrough),
            ServiceDocumentation = Field("serviceDocumentation", prior.ServiceDocumentation, edits.ServiceDocumentation, proposal.ServiceDocumentation),
            LastServiceDate = Field("lastServiceDate", prior.LastServiceDate, edits.LastServiceDate, proposal.LastServiceDate),
            LastServiceOdometerKilometres = Field("lastServiceOdometerKilometres", prior.LastServiceOdometerKilometres, edits.LastServiceOdometerKilometres, proposal.LastServiceOdometerKilometres),
            ServiceNotes = Field("serviceNotes", prior.ServiceNotes, edits.ServiceNotes, proposal.ServiceNotes),
        };
        IReadOnlyList<VehicleFact<string>>? notes;
        if (edits.ConditionNotes is null)
        {
            notes = current?.ConditionNotes;
            for (var i = 0; i < (notes?.Count ?? 0); i++)
            {
                var key = $"conditionNotes[{i}]";
                versions[key] = current!.ObservationListingVersions.GetValueOrDefault(key) ?? Array.AsReadOnly(new long?[notes![i].Observations.Count]);
            }
        }
        else
        {
            if (edits.ConditionNotes.Count > 10) throw Error("conditionNotes", "tooManyItems", "At most ten condition notes are allowed.");
            notes = edits.ConditionNotes.Select((edit, i) => Field($"conditionNotes[{i}]",
                current?.ConditionNotes?.ElementAtOrDefault(i), edit ?? throw Error($"conditionNotes[{i}]", "required", "An operation is required."),
                listing?.ConditionNotes?.ElementAtOrDefault(i))).ToArray();
        }
        return new(facts, versions, notes);
    }

    public static ComparisonFactSet Normalize(ComparisonFactSet input, AcquisitionType acquisitionType)
    {
        var processor = new VehicleFactsProcessor();
        var facts = processor.Normalize(input.Facts, acquisitionType);
        if (input.ConditionNotes?.Count > 10) throw Error("conditionNotes", "tooManyItems", "At most ten notes are allowed.");
        var notes = input.ConditionNotes?.Select((note, i) =>
        {
            VehicleFact<string> value;
            try { value = processor.Normalize(new() { ServiceNotes = note }).ServiceNotes!; }
            catch (VehicleFactsValidationException e)
            {
                throw new ComparisonInputValidationException(e.Errors.Select(x => new ComparisonInputError(
                    $"conditionNotes[{i}]" + x.Path["serviceNotes".Length..], x.Code, x.Message)));
            }
            if (value.Observations.Any(x => x.Value.Length > 300)) throw Error($"conditionNotes[{i}]", "tooLong", "A condition note is limited to 300 characters.");
            return value;
        }).ToArray();
        return new(facts, input.ObservationListingVersions, notes);
    }

    public static IReadOnlyList<LegacyReviewItem> ResolveReviews(IReadOnlyList<LegacyReviewItem> source,
        VehicleCostInput? input, IReadOnlyList<LegacyItemDecision>? decisions)
    {
        if (input is null)
        {
            if (decisions?.Count > 0) throw Error("legacyDecisions", "costInputRequired", "Review decisions require cost inputs.");
            return source;
        }
        return LegacyInputRecovery.Resolve(source, new(input, LegacyDecisions: decisions), requireDecisions: false);
    }

    public static ComparisonReviewItem ToCoreReview(LegacyReviewItem item) => new(item.Key, item.Reason,
        item.AffectedSections.Select(x => x switch
        {
            "energy" => ComparisonReviewSection.Energy, "tax" => ComparisonReviewSection.Tax,
            "insurance" => ComparisonReviewSection.Insurance, "service" => ComparisonReviewSection.Service,
            "repairs" => ComparisonReviewSection.Repairs, "customCosts" => ComparisonReviewSection.CustomCosts,
            "ownership" => ComparisonReviewSection.Ownership, "payments" => ComparisonReviewSection.Payments,
            "startupBudget" => ComparisonReviewSection.StartupBudget, "monthlyBudget" => ComparisonReviewSection.MonthlyBudget,
            _ => throw new InvalidOperationException("Unsupported trusted review section."),
        }));

    public static ComparisonInputValidationException Error(string path, string code, string message) => new([new(path, code, message)]);
}
