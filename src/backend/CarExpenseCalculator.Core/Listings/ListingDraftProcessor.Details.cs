using System.Globalization;
using System.Text;

namespace CarExpenseCalculator.Core.Listings;

public sealed partial class ListingDraftProcessor
{
    private static ListingDetails? NormalizeDetails(ListingDetails? x, ListingUrl url, ProcessingMode mode,
        ICollection<ListingValidationError> errors)
    {
        if (x is null) return null;
        return new ListingDetails
        {
            Title = DetailValue(x.Title, "details.title", url, mode, errors, v => NormalizeString(v, 500)),
            Subtitle = DetailValue(x.Subtitle, "details.subtitle", url, mode, errors, v => NormalizeString(v, 500)),
            Description = DetailValue(x.Description, "details.description", url, mode, errors, NormalizeDescription),
            ListingId = DetailValue(x.ListingId, "details.listingId", url, mode, errors, v => NormalizeString(v, 100)),
            Seats = DetailValue(x.Seats, "details.seats", url, mode, errors, v => NormalizeInteger(v, 1, 100)),
            Doors = DetailValue(x.Doors, "details.doors", url, mode, errors, v => NormalizeInteger(v, 0, 100)),
            LuggageLitres = DetailValue(x.LuggageLitres, "details.luggageLitres", url, mode, errors, v => NormalizeDecimal(v, 0, 100000)),
            WeightKilograms = DetailValue(x.WeightKilograms, "details.weightKilograms", url, mode, errors, v => NormalizeDecimal(v, 0, 1000000)),
            WeightLabel = DetailValue(x.WeightLabel, "details.weightLabel", url, mode, errors, v => NormalizeString(v, 100)),
            WeightCategory = DetailValue(x.WeightCategory, "details.weightCategory", url, mode, errors, v => NormalizeCategory(v, ["unspecified", "curb", "gross"])),
            TrailerWeightKilograms = DetailValue(x.TrailerWeightKilograms, "details.trailerWeightKilograms", url, mode, errors, v => NormalizeDecimal(v, 0, 100000)),
            TrailerWeightLabel = DetailValue(x.TrailerWeightLabel, "details.trailerWeightLabel", url, mode, errors, v => NormalizeString(v, 100)),
            TrailerWeightCategory = DetailValue(x.TrailerWeightCategory, "details.trailerWeightCategory", url, mode, errors, v => NormalizeCategory(v, ["unspecified", "braked", "unbraked"])),
            PostalCode = DetailValue(x.PostalCode, "details.postalCode", url, mode, errors, v => NormalizeString(v, 100)),
            Country = DetailValue(x.Country, "details.country", url, mode, errors, v => NormalizeString(v, 100)),
            FeeClass = DetailValue(x.FeeClass, "details.feeClass", url, mode, errors, v => NormalizeString(v, 100)),
            SaleForm = DetailValue(x.SaleForm, "details.saleForm", url, mode, errors, v => NormalizeString(v, 100)),
            UpdatedLocalDateTime = DetailValue(x.UpdatedLocalDateTime, "details.updatedLocalDateTime", url, mode, errors, NormalizeLocalDateTime),
            UpdatedTimeZone = DetailValue(x.UpdatedTimeZone, "details.updatedTimeZone", url, mode, errors, v => NormalizeString(v, 100)),
            UpdatedUtcOffsetMinutes = DetailValue(x.UpdatedUtcOffsetMinutes, "details.updatedUtcOffsetMinutes", url, mode, errors, v => NormalizeInteger(v, -840, 840)),
            Specifications = DetailEntries(x.Specifications, "details.specifications", url, mode, errors,
                v => NormalizePair(v.Name, v.Value, (a, b) => new ListingSpecification(a, b))),
            SellerAnswers = DetailEntries(x.SellerAnswers, "details.sellerAnswers", url, mode, errors,
                v => NormalizePair(v.Question, v.Answer, (a, b) => new SellerAnswer(a, b), 1000)),
        };
    }

    private static SourcedValue<T>? DetailValue<T>(SourcedValue<T>? x, string path, ListingUrl url,
        ProcessingMode mode, ICollection<ListingValidationError> errors, Func<T, Normalization<T>> normalize) where T : notnull
    {
        if (x is null) return null;
        var provenance = NormalizeProvenance(x.Provenance, path, url, mode, errors);
        if (provenance is null) { AddError(errors, path + ".provenance", "Invalid field provenance."); return null; }
        var value = normalize(x.Value);
        if (!value.IsValid) { AddError(errors, path, value.Error!); return null; }
        return new(value.Value!, provenance);
    }

    private static IReadOnlyList<SourcedValue<T>>? DetailEntries<T>(IReadOnlyList<SourcedValue<T>>? values,
        string path, ListingUrl url, ProcessingMode mode, ICollection<ListingValidationError> errors,
        Func<T, Normalization<T>> normalize) where T : notnull
    {
        if (values is null) return null;
        if (values.Count > ListingDetailLimits.Entries)
        { AddError(errors, path, "At most 100 entries are allowed."); return null; }
        var result = new List<SourcedValue<T>>();
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] is null) { AddError(errors, $"{path}[{i}]", "Entry is required."); continue; }
            var item = DetailValue(values[i], $"{path}[{i}]", url, mode, errors, normalize);
            if (item is not null) result.Add(item);
        }
        return Array.AsReadOnly(result.ToArray());
    }

    private static Normalization<T> NormalizePair<T>(string a, string b, Func<string, string, T> create, int labelLength = 100) where T : notnull
    {
        var first = NormalizeString(a, labelLength);
        var second = NormalizeString(b, ListingDetailLimits.EntryTextLength);
        return first.IsValid && second.IsValid ? Normalization<T>.Valid(create(first.Value!, second.Value!))
            : Normalization<T>.Invalid(first.Error ?? second.Error!);
    }

    private static Normalization<string> NormalizeDescription(string value)
    {
        var normalized = (value ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Trim().Normalize(NormalizationForm.FormC);
        return normalized.Length is > 0 and <= ListingDetailLimits.DescriptionLength
            ? Normalization<string>.Valid(normalized)
            : Normalization<string>.Invalid("Description must contain 1 through 32000 characters.");
    }

    private static Normalization<string> NormalizeCategory(string value, string[] allowed)
    {
        var normalized = NormalizeText(value);
        return allowed.Contains(normalized, StringComparer.Ordinal) ? Normalization<string>.Valid(normalized)
            : Normalization<string>.Invalid("Weight category is unsupported.");
    }

    private static Normalization<string> NormalizeLocalDateTime(string value) =>
        DateTime.TryParseExact(value, ["yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss"], CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var timestamp)
            ? Normalization<string>.Valid(timestamp.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture))
            : Normalization<string>.Invalid("Use a local date/time without a timezone; timezone and offset are separate optional facts.");
}
