namespace CarExpenseCalculator.Core.Households;

public enum SensitivityMode
{
    Baseline,
    Favorable,
    Cautious,
}

// Factories make a single value and a complete trio mutually exclusive.
// An unknown assumption is a null SensitivityValue, not an incomplete trio.
public sealed record SensitivityValue
{
    private SensitivityValue(decimal? single, decimal? favorable, decimal? baseline, decimal? cautious)
    {
        Single = single;
        Favorable = favorable;
        Baseline = baseline;
        Cautious = cautious;
    }

    public decimal? Single { get; }
    public decimal? Favorable { get; }
    public decimal? Baseline { get; }
    public decimal? Cautious { get; }

    public static SensitivityValue Constant(decimal value) => new(value, null, null, null);

    public static SensitivityValue Scenarios(decimal favorable, decimal baseline, decimal cautious) =>
        new(null, favorable, baseline, cautious);

    public decimal GetValue(SensitivityMode mode)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        return Single ?? (mode switch
        {
            SensitivityMode.Favorable => Favorable!.Value,
            SensitivityMode.Baseline => Baseline!.Value,
            SensitivityMode.Cautious => Cautious!.Value,
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        });
    }
}
