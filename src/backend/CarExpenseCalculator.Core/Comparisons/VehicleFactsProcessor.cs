using System.Globalization;
using System.Text;
using CarExpenseCalculator.Core.Households;
using CarExpenseCalculator.Core.Listings;

namespace CarExpenseCalculator.Core.Comparisons;

public sealed class VehicleFactsProcessor
{
    public VehicleComparisonFacts Normalize(VehicleComparisonFacts input, AcquisitionType acquisitionType = AcquisitionType.Purchase)
    {
        ArgumentNullException.ThrowIfNull(input);
        var errors = new List<VehicleFactsValidationError>();
        if (!Enum.IsDefined(acquisitionType))
        {
            Add(errors, "acquisitionType", "invalidEnum", "Acquisition type is not supported.");
        }

        var normalized = new VehicleComparisonFacts
        {
            PurchasePriceSek = Fact(input.PurchasePriceSek, "purchasePriceSek", errors,
                (value, path) => Range(value, 0, VehicleFactLimits.MaximumPriceSek, path, errors)),
            OdometerKilometres = Fact(input.OdometerKilometres, "odometerKilometres", errors,
                (value, path) => Range(value, 0, VehicleFactLimits.MaximumOdometerKilometres, path, errors)),
            OwnerCount = Fact(input.OwnerCount, "ownerCount", errors,
                (value, path) => Integer(value, 0, VehicleFactLimits.MaximumOwners, path, errors)),
            TowBar = Fact(input.TowBar, "towBar", errors, (value, _) => value),
            Transmission = Fact(input.Transmission, "transmission", errors, (value, path) => EnumValue(value, path, errors)),
            Seats = Fact(input.Seats, "seats", errors,
                (value, path) => Integer(value, VehicleFactLimits.MinimumSeats, VehicleFactLimits.MaximumSeats, path, errors)),
            ModelYear = Fact(input.ModelYear, "modelYear", errors,
                (value, path) => Integer(value, VehicleFactLimits.MinimumModelYear, VehicleFactLimits.MaximumModelYear, path, errors)),
            FuelTypes = Fact(input.FuelTypes, "fuelTypes", errors, (value, path) => Fuels(value, path, errors)),
            BodyType = Fact(input.BodyType, "bodyType", errors, (value, path) => EnumValue(value, path, errors)),
            Drivetrain = Fact(input.Drivetrain, "drivetrain", errors, (value, path) => EnumValue(value, path, errors)),
            Locality = Fact(input.Locality, "locality", errors,
                (value, path) => Text(value, VehicleFactLimits.MaximumLocationLength, path, errors), StringComparer.OrdinalIgnoreCase),
            County = Fact(input.County, "county", errors,
                (value, path) => Text(value, VehicleFactLimits.MaximumLocationLength, path, errors), StringComparer.OrdinalIgnoreCase),
            TowingCapacityKilograms = Fact(input.TowingCapacityKilograms, "towingCapacityKilograms", errors,
                (value, path) => Integer(value, 0, VehicleFactLimits.MaximumTowingCapacityKilograms, path, errors)),
            InspectionValidThrough = Fact(input.InspectionValidThrough, "inspectionValidThrough", errors, (value, _) => value),
            ServiceDocumentation = Fact(input.ServiceDocumentation, "serviceDocumentation", errors,
                (value, path) => EnumValue(value, path, errors)),
            LastServiceDate = Fact(input.LastServiceDate, "lastServiceDate", errors, (value, _) => value),
            LastServiceOdometerKilometres = Fact(input.LastServiceOdometerKilometres, "lastServiceOdometerKilometres", errors,
                (value, path) => Range(value, 0, VehicleFactLimits.MaximumOdometerKilometres, path, errors)),
            ServiceNotes = Fact(input.ServiceNotes, "serviceNotes", errors,
                (value, path) => Text(value, VehicleFactLimits.MaximumNotesLength, path, errors)),
        };

        if (errors.Count > 0)
        {
            throw new VehicleFactsValidationException(errors);
        }

        return acquisitionType == AcquisitionType.Lease && normalized.PurchasePriceSek.State == VehicleFactState.Unknown
            ? normalized with { PurchasePriceSek = VehicleFact<decimal>.NotApplicable() }
            : normalized;
    }

    // Re-run the established source boundary; merely importing does not confirm a value.
    public VehicleComparisonFacts FromReviewedListing(ListingUrl submittedUrl, IEnumerable<ListingUrl> returnedSources,
        ListingDraft input, AcquisitionType acquisitionType = AcquisitionType.Purchase)
    {
        ArgumentNullException.ThrowIfNull(input);
        // Reject invalid supplied comparison values rather than silently losing them in
        // the listing processor's existing best-effort normalization of AI values.
        Normalize(Map(input), acquisitionType);
        ListingDraft reviewed;
        try
        {
            reviewed = new ListingDraftProcessor().ProcessReviewed(submittedUrl, returnedSources, input).Listing;
        }
        catch (ListingValidationException exception)
        {
            throw new VehicleFactsValidationException(exception.Errors.Select(error =>
                new VehicleFactsValidationError(error.Path, "invalidListing", error.Message)));
        }

        return Normalize(Map(reviewed), acquisitionType);
    }

    private static VehicleComparisonFacts Map(ListingDraft listing) => new()
    {
        PurchasePriceSek = Map(listing.PriceSek),
        OdometerKilometres = Map(listing.OdometerKilometres),
        OwnerCount = Map(listing.OwnerCount),
        TowBar = Map(listing.TowBar),
        Transmission = Map(listing.Transmission),
        ModelYear = Map(listing.ModelYear),
        FuelTypes = listing.FuelTypes is null ? null : VehicleFact<FuelTypeSet>.Known(
            new FuelTypeSet(listing.FuelTypes.Values), Evidence(listing.FuelTypes.Provenance)),
        BodyType = Map(listing.BodyType),
        Drivetrain = Map(listing.Drivetrain),
        Locality = Map(listing.Locality),
        County = Map(listing.County),
        // Last/next inspection are not explicit validity. Equipment and free text are
        // not typed seats, braked towing capacity, or service documentation.
    };

    private static VehicleFact<T>? Map<T>(SourcedValue<T>? value) where T : notnull => value is null
        ? null : VehicleFact<T>.Known(value.Value, Evidence(value.Provenance));

    private static ComparisonEvidence Evidence(FieldProvenance? provenance) => provenance is null
        ? null! // Preserve invalid supplied metadata for the typed validator.
        : new(provenance.Origin, provenance.ExtractionMethod, provenance.Verification, provenance.SourceUrl);

    private static VehicleFact<T> Fact<T>(VehicleFact<T>? fact, string path, List<VehicleFactsValidationError> errors,
        Func<T, string, T> normalizeValue, IEqualityComparer<T>? comparer = null) where T : notnull
    {
        if (fact is null)
        {
            return VehicleFact<T>.Unknown();
        }

        var validState = fact.State switch
        {
            VehicleFactState.Known => fact.Observations.Count == 1,
            VehicleFactState.Unknown or VehicleFactState.NotApplicable => fact.Observations.Count == 0,
            VehicleFactState.Conflicting => fact.Observations.Count >= 2,
            _ => false,
        };
        if (!validState)
        {
            Add(errors, $"{path}.state", "invalidState", "Fact state and observations are inconsistent.");
        }

        var observations = new List<FactObservation<T>>();
        for (var index = 0; index < fact.Observations.Count; index++)
        {
            var observation = fact.Observations[index];
            var observationPath = $"{path}.observations[{index}]";
            if (observation is null)
            {
                Add(errors, observationPath, "required", "An observation cannot be null.");
                continue;
            }

            ValidateEvidence(observation.Evidence, $"{observationPath}.evidence", errors);
            if (observation.Value is null)
            {
                Add(errors, $"{observationPath}.value", "required", "A supplied observation must have a value.");
                continue;
            }

            observations.Add(new(normalizeValue(observation.Value, $"{observationPath}.value"), observation.Evidence));
        }

        if (validState && fact.State == VehicleFactState.Conflicting &&
            observations.Select(observation => observation.Value).Distinct(comparer).Count() < 2)
        {
            Add(errors, $"{path}.observations", "invalidConflict", "A conflict requires different normalized current values.");
        }

        return new(fact.State, observations);
    }

    private static void ValidateEvidence(ComparisonEvidence? evidence, string path, List<VehicleFactsValidationError> errors)
    {
        if (evidence is null)
        {
            Add(errors, path, "required", "Supplied values require evidence metadata.");
            return;
        }

        var listing = evidence.Origin == FieldOrigin.Listing && evidence.ExtractionMethod == ExtractionMethod.Ai &&
            evidence.Verification == VerificationStatus.Unverified;
        var manual = evidence.Origin == FieldOrigin.User && evidence.ExtractionMethod == ExtractionMethod.Manual &&
            evidence.Verification == VerificationStatus.UserConfirmed;
        if (!listing && !manual)
        {
            Add(errors, path, "unsupportedEvidence", "Only listing/ai/unverified and user/manual/userConfirmed evidence is supported.");
        }

        if (listing && evidence.SourceUrl is null)
        {
            Add(errors, $"{path}.sourceUrl", "required", "A listing observation requires its listing URL.");
        }

        if (evidence.Verification != VerificationStatus.UserConfirmed && evidence.ConfirmedAt is not null)
        {
            Add(errors, $"{path}.confirmedAt", "invalidEvidence", "Unconfirmed evidence cannot carry a user confirmation time.");
        }
    }

    private static T EnumValue<T>(T value, string path, List<VehicleFactsValidationError> errors) where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            Add(errors, path, "invalidEnum", "Value is not supported.");
        }

        return value;
    }

    private static FuelTypeSet Fuels(FuelTypeSet value, string path, List<VehicleFactsValidationError> errors)
    {
        var seen = new HashSet<FuelType>();
        for (var index = 0; index < value.Values.Count; index++)
        {
            var itemPath = $"{path}.values[{index}]";
            EnumValue(value.Values[index], itemPath, errors);
            if (!seen.Add(value.Values[index]))
            {
                Add(errors, itemPath, "duplicateValue", "Duplicate fuel types are not allowed.");
            }
        }

        return new(value.Values);
    }

    private static string Text(string value, int maximumLength, string path, List<VehicleFactsValidationError> errors)
    {
        string normalized;
        try
        {
            normalized = value.Trim().Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            Add(errors, path, "invalidText", "Text must contain valid Unicode.");
            return value;
        }

        if (normalized.Length == 0)
        {
            Add(errors, path, "required", "A supplied text value cannot be empty.");
        }
        else if (normalized.Length > maximumLength)
        {
            Add(errors, path, "tooLong", $"Text cannot exceed {maximumLength} characters.");
        }

        return normalized;
    }

    private static decimal Range(decimal value, decimal minimum, decimal maximum, string path,
        List<VehicleFactsValidationError> errors)
    {
        if (value < minimum || value > maximum)
        {
            Add(errors, path, "outOfRange", string.Create(CultureInfo.InvariantCulture,
                $"Value must be between {minimum} and {maximum} inclusive."));
        }

        return value;
    }

    private static int Integer(int value, int minimum, int maximum, string path, List<VehicleFactsValidationError> errors)
    {
        Range(value, minimum, maximum, path, errors);
        return value;
    }

    private static void Add(List<VehicleFactsValidationError> errors, string path, string code, string message) =>
        errors.Add(new(path, code, message));
}
