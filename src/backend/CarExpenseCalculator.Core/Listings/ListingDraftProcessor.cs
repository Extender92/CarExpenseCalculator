using System.Globalization;
using System.Text;
using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Core.Vehicles;

namespace CarExpenseCalculator.Core.Listings;

public sealed class ListingDraftProcessor
{
    private const decimal MaximumMoneySek = 100_000_000m;
    private const decimal MaximumOdometerKilometres = 10_000_000m;
    private const decimal MaximumEngineDisplacement = 100_000m;
    private const decimal MaximumConsumption = 10_000m;
    private const int MaximumGeneralLabelLength = 100;
    private const int MaximumVinLength = 50;
    private const int MaximumEquipmentCount = 100;
    private const int MaximumSellerClaimCount = 20;
    private const int MaximumConditionNoteCount = 10;
    private const int MaximumSellerClaimLength = 200;
    private const int MaximumConditionNoteLength = 300;
    private const int MaximumEnergyConsumptionCount = 2;

    public ListingProcessingResult ProcessExtraction(
        ListingUrl submittedUrl,
        IEnumerable<ListingUrl> returnedSources,
        ListingDraft draft)
    {
        return Process(submittedUrl, returnedSources, draft, ProcessingMode.Extraction);
    }

    public ListingProcessingResult ProcessReviewed(
        ListingUrl submittedUrl,
        IEnumerable<ListingUrl> returnedSources,
        ListingDraft draft)
    {
        return Process(submittedUrl, returnedSources, draft, ProcessingMode.Reviewed);
    }

    private static ListingProcessingResult Process(
        ListingUrl submittedUrl,
        IEnumerable<ListingUrl> returnedSources,
        ListingDraft draft,
        ProcessingMode mode)
    {
        ArgumentNullException.ThrowIfNull(submittedUrl);
        ArgumentNullException.ThrowIfNull(returnedSources);
        ArgumentNullException.ThrowIfNull(draft);

        var errors = new List<ListingValidationError>();
        var sources = NormalizeSources(submittedUrl, returnedSources, errors);
        var hasMatchedSource = sources.Any(source => source.MatchesSubmittedUrl);
        var normalized = NormalizeDraft(draft, submittedUrl, hasMatchedSource, mode, errors);

        if (errors.Count > 0)
        {
            throw new ListingValidationException(errors);
        }

        var missingFields = CreateMissingFields(normalized);
        var status = Classify(normalized, hasMatchedSource);
        return new ListingProcessingResult(
            status,
            sources,
            normalized,
            missingFields);
    }

    private static IReadOnlyList<ListingAnalysisSource> NormalizeSources(
        ListingUrl submittedUrl,
        IEnumerable<ListingUrl> returnedSources,
        ICollection<ListingValidationError> errors)
    {
        var sources = new List<ListingAnalysisSource>();
        var index = 0;
        foreach (var source in returnedSources)
        {
            if (source is null)
            {
                AddError(errors, $"sources[{index}]", "Source URL cannot be null.");
            }
            else
            {
                sources.Add(new ListingAnalysisSource(source, source.IsSourceMatchFor(submittedUrl)));
            }

            index++;
        }

        return Array.AsReadOnly(sources.ToArray());
    }

    private static ListingDraft NormalizeDraft(
        ListingDraft draft,
        ListingUrl submittedUrl,
        bool hasMatchedSource,
        ProcessingMode mode,
        ICollection<ListingValidationError> errors)
    {
        return new ListingDraft
        {
            RegistrationNumber = NormalizeValue(
                draft.RegistrationNumber,
                "registrationNumber",
                submittedUrl,
                hasMatchedSource,
                mode,
                errors,
                NormalizeRegistrationNumber),
            Make = NormalizeGeneralLabel(
                draft.Make,
                "make",
                submittedUrl,
                hasMatchedSource,
                mode,
                errors),
            Model = NormalizeGeneralLabel(
                draft.Model,
                "model",
                submittedUrl,
                hasMatchedSource,
                mode,
                errors),
            Variant = NormalizeGeneralLabel(
                draft.Variant,
                "variant",
                submittedUrl,
                hasMatchedSource,
                mode,
                errors),
            ModelYear = NormalizeValue(
                draft.ModelYear,
                "modelYear",
                submittedUrl,
                hasMatchedSource,
                mode,
                errors,
                value => NormalizeInteger(value, 1886, 2100)),
            Vin = NormalizeValue(
                draft.Vin,
                "vin",
                submittedUrl,
                hasMatchedSource,
                mode,
                errors,
                NormalizeVin),
            VehicleLabel = NormalizeGeneralLabel(
                draft.VehicleLabel,
                "vehicleLabel",
                submittedUrl,
                hasMatchedSource,
                mode,
                errors,
                allowAi: false),
            PriceSek = NormalizeValue(
                draft.PriceSek,
                "priceSek",
                submittedUrl,
                hasMatchedSource,
                mode,
                errors,
                value => NormalizeDecimal(value, 0m, MaximumMoneySek)),
            OdometerKilometres = NormalizeValue(
                draft.OdometerKilometres,
                "odometerKilometres",
                submittedUrl,
                hasMatchedSource,
                mode,
                errors,
                value => NormalizeDecimal(value, 0m, MaximumOdometerKilometres)),
            SellerType = NormalizeEnumValue(
                draft.SellerType,
                "sellerType",
                submittedUrl,
                hasMatchedSource,
                mode,
                errors),
            Locality = NormalizeGeneralLabel(
                draft.Locality,
                "locality",
                submittedUrl,
                hasMatchedSource,
                mode,
                errors),
            County = NormalizeGeneralLabel(
                draft.County,
                "county",
                submittedUrl,
                hasMatchedSource,
                mode,
                errors),
            PublishedDate = NormalizeSimpleValue(
                draft.PublishedDate,
                "publishedDate",
                submittedUrl,
                hasMatchedSource,
                mode,
                errors),
            UpdatedDate = NormalizeSimpleValue(
                draft.UpdatedDate,
                "updatedDate",
                submittedUrl,
                hasMatchedSource,
                mode,
                errors),
            ImageCount = NormalizeValue(
                draft.ImageCount,
                "imageCount",
                submittedUrl,
                hasMatchedSource,
                mode,
                errors,
                value => NormalizeInteger(value, 0, 10_000)),
            FuelTypes = NormalizeEnumCollection(
                draft.FuelTypes,
                "fuelTypes",
                submittedUrl,
                hasMatchedSource,
                mode,
                errors),
            Transmission = NormalizeEnumValue(
                draft.Transmission,
                "transmission",
                submittedUrl,
                hasMatchedSource,
                mode,
                errors),
            Drivetrain = NormalizeEnumValue(
                draft.Drivetrain,
                "drivetrain",
                submittedUrl,
                hasMatchedSource,
                mode,
                errors),
            BodyType = NormalizeEnumValue(
                draft.BodyType,
                "bodyType",
                submittedUrl,
                hasMatchedSource,
                mode,
                errors),
            Colour = NormalizeGeneralLabel(
                draft.Colour,
                "colour",
                submittedUrl,
                hasMatchedSource,
                mode,
                errors),
            Horsepower = NormalizeValue(
                draft.Horsepower,
                "horsepower",
                submittedUrl,
                hasMatchedSource,
                mode,
                errors,
                value => NormalizeInteger(value, 1, 10_000)),
            EngineDisplacementCubicCentimetres = NormalizeValue(
                draft.EngineDisplacementCubicCentimetres,
                "engineDisplacementCubicCentimetres",
                submittedUrl,
                hasMatchedSource,
                mode,
                errors,
                value => NormalizeDecimal(value, 1m, MaximumEngineDisplacement)),
            EnergyConsumptions = NormalizeEnergyConsumptions(
                draft.EnergyConsumptions,
                "energyConsumptions",
                submittedUrl,
                hasMatchedSource,
                mode,
                errors),
            AnnualVehicleTaxSek = NormalizeValue(
                draft.AnnualVehicleTaxSek,
                "annualVehicleTaxSek",
                submittedUrl,
                hasMatchedSource,
                mode,
                errors,
                value => NormalizeDecimal(value, 0m, MaximumMoneySek)),
            OwnerCount = NormalizeValue(
                draft.OwnerCount,
                "ownerCount",
                submittedUrl,
                hasMatchedSource,
                mode,
                errors,
                value => NormalizeInteger(value, 0, 10_000)),
            FirstRegistrationDate = NormalizeSimpleValue(
                draft.FirstRegistrationDate,
                "firstRegistrationDate",
                submittedUrl,
                hasMatchedSource,
                mode,
                errors),
            LastInspectionDate = NormalizeSimpleValue(
                draft.LastInspectionDate,
                "lastInspectionDate",
                submittedUrl,
                hasMatchedSource,
                mode,
                errors),
            NextInspectionDate = NormalizeSimpleValue(
                draft.NextInspectionDate,
                "nextInspectionDate",
                submittedUrl,
                hasMatchedSource,
                mode,
                errors),
            TowBar = NormalizeSimpleValue(
                draft.TowBar,
                "towBar",
                submittedUrl,
                hasMatchedSource,
                mode,
                errors),
            Equipment = NormalizeStringCollection(
                draft.Equipment,
                "equipment",
                submittedUrl,
                hasMatchedSource,
                mode,
                errors,
                MaximumEquipmentCount,
                MaximumGeneralLabelLength),
            SellerClaims = NormalizeStringCollection(
                draft.SellerClaims,
                "sellerClaims",
                submittedUrl,
                hasMatchedSource,
                mode,
                errors,
                MaximumSellerClaimCount,
                MaximumSellerClaimLength),
            ConditionNotes = NormalizeStringCollection(
                draft.ConditionNotes,
                "conditionNotes",
                submittedUrl,
                hasMatchedSource,
                mode,
                errors,
                MaximumConditionNoteCount,
                MaximumConditionNoteLength),
        };
    }

    private static SourcedValue<string>? NormalizeGeneralLabel(
        SourcedValue<string>? value,
        string path,
        ListingUrl submittedUrl,
        bool hasMatchedSource,
        ProcessingMode mode,
        ICollection<ListingValidationError> errors,
        bool allowAi = true)
    {
        return NormalizeValue(
            value,
            path,
            submittedUrl,
            hasMatchedSource,
            mode,
            errors,
            input => NormalizeString(input, MaximumGeneralLabelLength),
            allowAi);
    }

    private static SourcedValue<T>? NormalizeSimpleValue<T>(
        SourcedValue<T>? value,
        string path,
        ListingUrl submittedUrl,
        bool hasMatchedSource,
        ProcessingMode mode,
        ICollection<ListingValidationError> errors)
        where T : notnull
    {
        return NormalizeValue(
            value,
            path,
            submittedUrl,
            hasMatchedSource,
            mode,
            errors,
            input => input is null
                ? Normalization<T>.Invalid("Value cannot be null.")
                : Normalization<T>.Valid(input));
    }

    private static SourcedValue<TEnum>? NormalizeEnumValue<TEnum>(
        SourcedValue<TEnum>? value,
        string path,
        ListingUrl submittedUrl,
        bool hasMatchedSource,
        ProcessingMode mode,
        ICollection<ListingValidationError> errors)
        where TEnum : struct, Enum
    {
        return NormalizeValue(
            value,
            path,
            submittedUrl,
            hasMatchedSource,
            mode,
            errors,
            input => Enum.IsDefined(input)
                ? Normalization<TEnum>.Valid(input)
                : Normalization<TEnum>.Invalid("Value is not supported."));
    }

    private static SourcedValue<T>? NormalizeValue<T>(
        SourcedValue<T>? source,
        string path,
        ListingUrl submittedUrl,
        bool hasMatchedSource,
        ProcessingMode mode,
        ICollection<ListingValidationError> errors,
        Func<T, Normalization<T>> normalize,
        bool allowAi = true)
        where T : notnull
    {
        if (source is null)
        {
            return null;
        }

        var provenance = NormalizeProvenance(
            source.Provenance,
            path,
            submittedUrl,
            hasMatchedSource,
            mode,
            errors,
            allowAi);
        if (provenance is null)
        {
            return null;
        }

        if (source.Value is null)
        {
            HandleInvalidValue(
                mode,
                provenance,
                errors,
                $"{path}.value",
                "Value cannot be null.");
            return null;
        }

        var result = normalize(source.Value);
        if (!result.IsValid)
        {
            HandleInvalidValue(mode, provenance, errors, $"{path}.value", result.Error!);
            return null;
        }

        return new SourcedValue<T>(result.Value!, provenance);
    }

    private static SourcedCollection<TEnum>? NormalizeEnumCollection<TEnum>(
        SourcedCollection<TEnum>? source,
        string path,
        ListingUrl submittedUrl,
        bool hasMatchedSource,
        ProcessingMode mode,
        ICollection<ListingValidationError> errors)
        where TEnum : struct, Enum
    {
        if (source is null)
        {
            return null;
        }

        var provenance = NormalizeProvenance(
            source.Provenance,
            path,
            submittedUrl,
            hasMatchedSource,
            mode,
            errors);
        if (provenance is null)
        {
            return null;
        }

        var normalized = new List<TEnum>(source.Values.Count);
        var seen = new HashSet<TEnum>();
        for (var index = 0; index < source.Values.Count; index++)
        {
            var value = source.Values[index];
            if (!Enum.IsDefined(value))
            {
                HandleInvalidValue(
                    mode,
                    provenance,
                    errors,
                    $"{path}.values[{index}]",
                    "Value is not supported.");
                continue;
            }

            if (!seen.Add(value))
            {
                HandleInvalidValue(
                    mode,
                    provenance,
                    errors,
                    $"{path}.values[{index}]",
                    "Duplicate value is not allowed.");
                continue;
            }

            normalized.Add(value);
        }

        return CreateNormalizedCollection(source.Values.Count, normalized, provenance, mode);
    }

    private static SourcedCollection<string>? NormalizeStringCollection(
        SourcedCollection<string>? source,
        string path,
        ListingUrl submittedUrl,
        bool hasMatchedSource,
        ProcessingMode mode,
        ICollection<ListingValidationError> errors,
        int maximumCount,
        int maximumLength)
    {
        if (source is null)
        {
            return null;
        }

        var provenance = NormalizeProvenance(
            source.Provenance,
            path,
            submittedUrl,
            hasMatchedSource,
            mode,
            errors);
        if (provenance is null)
        {
            return null;
        }

        if (source.Values.Count > maximumCount)
        {
            HandleInvalidValue(
                mode,
                provenance,
                errors,
                $"{path}.values",
                $"At most {maximumCount} values are allowed.");
        }

        var countToProcess = mode == ProcessingMode.Extraction
            ? Math.Min(source.Values.Count, maximumCount)
            : source.Values.Count;
        var normalized = new List<string>(Math.Min(countToProcess, maximumCount));
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < countToProcess; index++)
        {
            var result = NormalizeString(source.Values[index], maximumLength);
            if (!result.IsValid)
            {
                HandleInvalidValue(
                    mode,
                    provenance,
                    errors,
                    $"{path}.values[{index}]",
                    result.Error!);
                continue;
            }

            if (!seen.Add(result.Value!))
            {
                HandleInvalidValue(
                    mode,
                    provenance,
                    errors,
                    $"{path}.values[{index}]",
                    "Duplicate value is not allowed.");
                continue;
            }

            normalized.Add(result.Value!);
        }

        return CreateNormalizedCollection(source.Values.Count, normalized, provenance, mode);
    }

    private static SourcedCollection<EnergyConsumption>? NormalizeEnergyConsumptions(
        SourcedCollection<EnergyConsumption>? source,
        string path,
        ListingUrl submittedUrl,
        bool hasMatchedSource,
        ProcessingMode mode,
        ICollection<ListingValidationError> errors)
    {
        if (source is null)
        {
            return null;
        }

        var provenance = NormalizeProvenance(
            source.Provenance,
            path,
            submittedUrl,
            hasMatchedSource,
            mode,
            errors);
        if (provenance is null)
        {
            return null;
        }

        if (source.Values.Count > MaximumEnergyConsumptionCount)
        {
            HandleInvalidValue(
                mode,
                provenance,
                errors,
                $"{path}.values",
                $"At most {MaximumEnergyConsumptionCount} energy consumptions are allowed.");
        }

        var countToProcess = mode == ProcessingMode.Extraction
            ? Math.Min(source.Values.Count, MaximumEnergyConsumptionCount)
            : source.Values.Count;
        var normalized = new List<EnergyConsumption>(Math.Min(countToProcess, MaximumEnergyConsumptionCount));
        for (var index = 0; index < countToProcess; index++)
        {
            var value = source.Values[index];
            var itemPath = $"{path}.values[{index}]";
            if (value is null)
            {
                HandleInvalidValue(
                    mode,
                    provenance,
                    errors,
                    itemPath,
                    "Energy consumption cannot be null.");
                continue;
            }

            var label = NormalizeString(value.Label, MaximumGeneralLabelLength);
            var unitIsValid = Enum.IsDefined(value.Unit);
            var consumptionIsValid = value.ConsumptionPer100Kilometres is > 0m and <= MaximumConsumption;
            if (!label.IsValid)
            {
                HandleInvalidValue(mode, provenance, errors, $"{itemPath}.label", label.Error!);
            }

            if (!unitIsValid)
            {
                HandleInvalidValue(
                    mode,
                    provenance,
                    errors,
                    $"{itemPath}.unit",
                    "Value is not supported.");
            }

            if (!consumptionIsValid)
            {
                HandleInvalidValue(
                    mode,
                    provenance,
                    errors,
                    $"{itemPath}.consumptionPer100Kilometres",
                    $"Value must be greater than 0 and at most {MaximumConsumption}.");
            }

            if (!label.IsValid || !unitIsValid || !consumptionIsValid)
            {
                continue;
            }

            if (normalized.Any(existing => existing.Label.Equals(label.Value, StringComparison.OrdinalIgnoreCase)))
            {
                HandleInvalidValue(
                    mode,
                    provenance,
                    errors,
                    $"{itemPath}.label",
                    "Duplicate value is not allowed.");
                continue;
            }

            normalized.Add(new EnergyConsumption(
                label.Value!,
                value.Unit,
                value.ConsumptionPer100Kilometres));
        }

        return CreateNormalizedCollection(source.Values.Count, normalized, provenance, mode);
    }

    private static SourcedCollection<T>? CreateNormalizedCollection<T>(
        int originalCount,
        IReadOnlyCollection<T> normalized,
        FieldProvenance provenance,
        ProcessingMode mode)
        where T : notnull
    {
        if (mode == ProcessingMode.Extraction && originalCount > 0 && normalized.Count == 0)
        {
            return null;
        }

        return new SourcedCollection<T>(normalized, provenance);
    }

    private static FieldProvenance? NormalizeProvenance(
        FieldProvenance? provenance,
        string path,
        ListingUrl submittedUrl,
        bool hasMatchedSource,
        ProcessingMode mode,
        ICollection<ListingValidationError> errors,
        bool allowAi = true)
    {
        if (provenance is null)
        {
            HandleInvalid(mode, errors, $"{path}.provenance", "Provenance is required.");
            return null;
        }

        var enumValuesAreValid = Enum.IsDefined(provenance.Origin)
            && Enum.IsDefined(provenance.ExtractionMethod)
            && Enum.IsDefined(provenance.Verification);
        var isAi = enumValuesAreValid
            && provenance.Origin == FieldOrigin.Listing
            && provenance.ExtractionMethod == ExtractionMethod.Ai
            && provenance.Verification == VerificationStatus.Unverified;
        var isManual = enumValuesAreValid
            && provenance.Origin == FieldOrigin.User
            && provenance.ExtractionMethod == ExtractionMethod.Manual
            && provenance.Verification == VerificationStatus.UserConfirmed;

        if (!enumValuesAreValid || (!isAi && !isManual))
        {
            HandleInvalid(
                mode,
                errors,
                $"{path}.provenance",
                "Only listing/ai/unverified and user/manual/userConfirmed provenance is supported.");
            return null;
        }

        if (mode == ProcessingMode.Extraction && !isAi)
        {
            return null;
        }

        if (isAi && (!allowAi || !hasMatchedSource))
        {
            return null;
        }

        var sourceMatches = provenance.SourceUrl is not null
            && (isAi
                ? provenance.SourceUrl.IsSourceMatchFor(submittedUrl)
                : provenance.SourceUrl.HasSamePageIdentity(submittedUrl));
        if (!sourceMatches)
        {
            if (isManual)
            {
                HandleInvalid(
                    mode,
                    errors,
                    $"{path}.provenance.sourceUrl",
                    "Provenance source must identify the submitted listing page.");
            }

            return null;
        }

        return new FieldProvenance(
            provenance.Origin,
            provenance.ExtractionMethod,
            provenance.Verification,
            submittedUrl);
    }

    private static Normalization<RegistrationNumber> NormalizeRegistrationNumber(
        RegistrationNumber registrationNumber)
    {
        return registrationNumber is null
            ? Normalization<RegistrationNumber>.Invalid("Registration number cannot be null.")
            : Normalization<RegistrationNumber>.Valid(registrationNumber);
    }

    private static Normalization<string> NormalizeVin(string value)
    {
        var normalized = NormalizeText(value);
        if (normalized.Length == 0)
        {
            return Normalization<string>.Invalid("Value must contain at least one non-whitespace character.");
        }

        if (normalized.Length > MaximumVinLength)
        {
            return Normalization<string>.Invalid($"Value cannot exceed {MaximumVinLength} characters.");
        }

        return Normalization<string>.Valid(normalized.ToUpperInvariant());
    }

    private static Normalization<string> NormalizeString(string? value, int maximumLength)
    {
        if (value is null)
        {
            return Normalization<string>.Invalid("Value cannot be null.");
        }

        var normalized = NormalizeText(value);
        if (normalized.Length == 0)
        {
            return Normalization<string>.Invalid("Value must contain at least one non-whitespace character.");
        }

        if (normalized.Length > maximumLength)
        {
            return Normalization<string>.Invalid($"Value cannot exceed {maximumLength} characters.");
        }

        return Normalization<string>.Valid(normalized);
    }

    private static string NormalizeText(string value)
    {
        return value.Trim().Normalize(NormalizationForm.FormC);
    }

    private static Normalization<int> NormalizeInteger(int value, int minimum, int maximum)
    {
        return value >= minimum && value <= maximum
            ? Normalization<int>.Valid(value)
            : Normalization<int>.Invalid($"Value must be between {minimum} and {maximum} inclusive.");
    }

    private static Normalization<decimal> NormalizeDecimal(decimal value, decimal minimum, decimal maximum)
    {
        return value >= minimum && value <= maximum
            ? Normalization<decimal>.Valid(value)
            : Normalization<decimal>.Invalid(
                $"Value must be between {minimum.ToString(CultureInfo.InvariantCulture)} and {maximum.ToString(CultureInfo.InvariantCulture)} inclusive.");
    }

    private static IReadOnlyList<ListingFieldCode> CreateMissingFields(ListingDraft listing)
    {
        var missing = new List<ListingFieldCode>(31);
        AddMissing(missing, listing.RegistrationNumber, ListingFieldCode.RegistrationNumber);
        AddMissing(missing, listing.Make, ListingFieldCode.Make);
        AddMissing(missing, listing.Model, ListingFieldCode.Model);
        AddMissing(missing, listing.Variant, ListingFieldCode.Variant);
        AddMissing(missing, listing.ModelYear, ListingFieldCode.ModelYear);
        AddMissing(missing, listing.Vin, ListingFieldCode.Vin);
        AddMissing(missing, listing.PriceSek, ListingFieldCode.PriceSek);
        AddMissing(missing, listing.OdometerKilometres, ListingFieldCode.OdometerKilometres);
        AddMissing(missing, listing.SellerType, ListingFieldCode.SellerType);
        AddMissing(missing, listing.Locality, ListingFieldCode.Locality);
        AddMissing(missing, listing.County, ListingFieldCode.County);
        AddMissing(missing, listing.PublishedDate, ListingFieldCode.PublishedDate);
        AddMissing(missing, listing.UpdatedDate, ListingFieldCode.UpdatedDate);
        AddMissing(missing, listing.ImageCount, ListingFieldCode.ImageCount);
        AddMissing(missing, listing.FuelTypes, ListingFieldCode.FuelTypes);
        AddMissing(missing, listing.Transmission, ListingFieldCode.Transmission);
        AddMissing(missing, listing.Drivetrain, ListingFieldCode.Drivetrain);
        AddMissing(missing, listing.BodyType, ListingFieldCode.BodyType);
        AddMissing(missing, listing.Colour, ListingFieldCode.Colour);
        AddMissing(missing, listing.Horsepower, ListingFieldCode.Horsepower);
        AddMissing(
            missing,
            listing.EngineDisplacementCubicCentimetres,
            ListingFieldCode.EngineDisplacementCubicCentimetres);
        AddMissing(missing, listing.EnergyConsumptions, ListingFieldCode.EnergyConsumptions);
        AddMissing(missing, listing.AnnualVehicleTaxSek, ListingFieldCode.AnnualVehicleTaxSek);
        AddMissing(missing, listing.OwnerCount, ListingFieldCode.OwnerCount);
        AddMissing(missing, listing.FirstRegistrationDate, ListingFieldCode.FirstRegistrationDate);
        AddMissing(missing, listing.LastInspectionDate, ListingFieldCode.LastInspectionDate);
        AddMissing(missing, listing.NextInspectionDate, ListingFieldCode.NextInspectionDate);
        AddMissing(missing, listing.TowBar, ListingFieldCode.TowBar);
        AddMissing(missing, listing.Equipment, ListingFieldCode.Equipment);
        AddMissing(missing, listing.SellerClaims, ListingFieldCode.SellerClaims);
        AddMissing(missing, listing.ConditionNotes, ListingFieldCode.ConditionNotes);
        return Array.AsReadOnly(missing.ToArray());
    }

    private static void AddMissing<T>(
        ICollection<ListingFieldCode> missing,
        T? value,
        ListingFieldCode code)
        where T : class
    {
        if (value is null)
        {
            missing.Add(code);
        }
    }

    private static ListingAnalysisStatus Classify(ListingDraft listing, bool hasMatchedSource)
    {
        if (!hasMatchedSource)
        {
            return ListingAnalysisStatus.Unavailable;
        }

        if (listing.RegistrationNumber is not null
            && listing.PriceSek is not null
            && listing.Make is not null
            && listing.Model is not null
            && listing.ModelYear is not null
            && listing.OdometerKilometres is not null)
        {
            return ListingAnalysisStatus.Complete;
        }

        return HasUsableExternallySourcedValue(listing)
            ? ListingAnalysisStatus.Partial
            : ListingAnalysisStatus.Unavailable;
    }

    private static bool HasUsableExternallySourcedValue(ListingDraft listing)
    {
        return listing.RegistrationNumber is not null
            || listing.Make is not null
            || listing.Model is not null
            || listing.Variant is not null
            || listing.ModelYear is not null
            || listing.Vin is not null
            || listing.PriceSek is not null
            || listing.OdometerKilometres is not null
            || listing.SellerType is not null
            || listing.Locality is not null
            || listing.County is not null
            || listing.PublishedDate is not null
            || listing.UpdatedDate is not null
            || listing.ImageCount is not null
            || listing.FuelTypes is not null
            || listing.Transmission is not null
            || listing.Drivetrain is not null
            || listing.BodyType is not null
            || listing.Colour is not null
            || listing.Horsepower is not null
            || listing.EngineDisplacementCubicCentimetres is not null
            || listing.EnergyConsumptions is not null
            || listing.AnnualVehicleTaxSek is not null
            || listing.OwnerCount is not null
            || listing.FirstRegistrationDate is not null
            || listing.LastInspectionDate is not null
            || listing.NextInspectionDate is not null
            || listing.TowBar is not null
            || listing.Equipment is not null
            || listing.SellerClaims is not null
            || listing.ConditionNotes is not null;
    }

    private static void HandleInvalid(
        ProcessingMode mode,
        ICollection<ListingValidationError> errors,
        string path,
        string message)
    {
        if (mode == ProcessingMode.Reviewed)
        {
            AddError(errors, path, message);
        }
    }

    private static void HandleInvalidValue(
        ProcessingMode mode,
        FieldProvenance provenance,
        ICollection<ListingValidationError> errors,
        string path,
        string message)
    {
        if (provenance.Origin == FieldOrigin.User)
        {
            HandleInvalid(mode, errors, path, message);
        }
    }

    private static void AddError(
        ICollection<ListingValidationError> errors,
        string path,
        string message)
    {
        errors.Add(new ListingValidationError(path, message));
    }

    private enum ProcessingMode
    {
        Extraction,
        Reviewed,
    }

    private readonly record struct Normalization<T>(
        bool IsValid,
        T? Value,
        string? Error)
        where T : notnull
    {
        public static Normalization<T> Valid(T value) => new(true, value, null);

        public static Normalization<T> Invalid(string error) => new(false, default, error);
    }
}
