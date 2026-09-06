namespace CarExpenseCalculator.Core.Households;

// Unrounded, calculation-local accumulator. Never compose display-rounded results.
internal sealed class CostSection(string path)
{
    public decimal? Known { get; private set; } = 0m;
    public bool HasKnown { get; private set; }
    public bool HasDetails { get; set; }
    public List<string> Missing { get; } = [];
    public List<HouseholdInputError> Errors { get; } = [];
    public bool IsComplete => Missing.Count == 0 && Errors.Count == 0 && Known is not null;
    public decimal? Complete => IsComplete ? Known : null;

    public void Add(decimal value)
    {
        HasKnown = true;
        if (Known is null) return;
        Known = Arithmetic(() => Known.Value + value);
    }

    public decimal? Arithmetic(Func<decimal> operation)
    {
        try { return operation(); }
        catch (OverflowException)
        {
            Errors.Add(new(path, "calculationOutOfRange", "Calculated value exceeds decimal capacity."));
            return null;
        }
    }

    public void Merge(CostSection section)
    {
        CopyProblems(section);
        HasDetails |= section.HasDetails;
        if (section.Known is null) Known = null;
        else if (section.HasKnown) Add(section.Known.Value);
    }

    public void CopyProblems(CostSection section)
    {
        Missing.AddRange(section.Missing);
        Errors.AddRange(section.Errors);
    }

    public void AddCalculated(Func<decimal> operation)
    {
        var value = Arithmetic(operation);
        if (value is not null) Add(value.Value);
        else Known = null;
    }

    public void AddTransformed(CostSection source, Func<decimal, decimal> transform)
    {
        if (source.Known is null) Known = null;
        else if (source.HasKnown) AddCalculated(() => transform(source.Known.Value));
    }

    public CostSectionResult Result() => new(
        Errors.Count > 0 ? CostSectionState.Invalid : IsComplete ? CostSectionState.Complete
            : HasKnown || HasDetails ? CostSectionState.Partial : CostSectionState.Unavailable,
        Money(Known), Money(Complete), Array.AsReadOnly(Missing.Distinct().ToArray()),
        Array.AsReadOnly(Errors.Distinct().ToArray()));

    public static decimal? Money(decimal? value) => value is null ? null : decimal.Round(value.Value, 2, MidpointRounding.AwayFromZero);
    public static decimal? Quantity(decimal? value) => value is null ? null : decimal.Round(value.Value, 3, MidpointRounding.AwayFromZero);
}

internal sealed class HouseholdCostContext(
    HouseholdProfileInput profile,
    IReadOnlyList<HouseholdInputError> profileErrors,
    IReadOnlyList<HouseholdInputError> vehicleErrors)
{
    public HouseholdProfileInput Profile => profile;

    public bool Available(string path, bool supplied, CostSection section)
    {
        var errors = (path.StartsWith("profile.", StringComparison.Ordinal) ? profileErrors : vehicleErrors)
            .Where(error => error.Path == path || error.Path.StartsWith(path + ".", StringComparison.Ordinal)).ToArray();
        section.Errors.AddRange(errors);
        if (!supplied) section.Missing.Add(path);
        return supplied && errors.Length == 0;
    }

    public decimal? Value(decimal? value, string path, CostSection section) => Available(path, value is not null, section) ? value : null;

    public decimal? Value(SensitivityValue? value, string path, CostSection section) =>
        Available(path, value is not null, section) ? value!.GetValue(profile.ActiveSensitivityMode) : null;

    public int? Period(CostSection section) => Available("profile.periodMonths", profile.PeriodMonths is not null, section)
        ? profile.PeriodMonths : null;
}
