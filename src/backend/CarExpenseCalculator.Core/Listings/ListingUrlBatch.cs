namespace CarExpenseCalculator.Core.Listings;

public sealed class ListingUrlBatch
{
    public const int MaximumCount = 10;

    private ListingUrlBatch(IReadOnlyList<ListingUrl> urls)
    {
        Urls = urls;
    }

    public IReadOnlyList<ListingUrl> Urls { get; }

    public static ListingUrlBatch Create(IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var inputs = values.ToArray();
        var errors = new List<ListingValidationError>();
        if (inputs.Length is < 1 or > MaximumCount)
        {
            errors.Add(new ListingValidationError(
                "urls",
                $"Between 1 and {MaximumCount} URLs are required."));
        }

        var urls = new List<ListingUrl>(Math.Min(inputs.Length, MaximumCount));
        var originalIndexes = new List<int>(urls.Capacity);
        for (var index = 0; index < inputs.Length; index++)
        {
            var value = inputs[index];
            if (value is null)
            {
                errors.Add(new ListingValidationError($"urls[{index}]", "URL cannot be null."));
                continue;
            }

            ListingUrl url;
            try
            {
                url = ListingUrl.Parse(value);
            }
            catch (ListingUrlValidationException exception)
            {
                errors.Add(new ListingValidationError($"urls[{index}]", exception.Message));
                continue;
            }

            var duplicateListIndex = urls.FindIndex(existing => existing.HasSamePageIdentity(url));
            if (duplicateListIndex >= 0)
            {
                errors.Add(new ListingValidationError(
                    $"urls[{index}]",
                    $"URL duplicates urls[{originalIndexes[duplicateListIndex]}] by page identity."));
                continue;
            }

            urls.Add(url);
            originalIndexes.Add(index);
        }

        if (errors.Count > 0)
        {
            throw new ListingValidationException(errors);
        }

        return new ListingUrlBatch(Array.AsReadOnly(urls.ToArray()));
    }
}
