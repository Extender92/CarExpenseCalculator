using System.Globalization;

namespace CarExpenseCalculator.Api.Comparisons;

internal sealed class CompleteComparisonLimits : IDisposable
{
    public const int DefaultMaximumRequestBytes = 32 * 1024 * 1024;
    public int MaximumRequestBytes { get; }
    public SemaphoreSlim Slots { get; } = new(2, 2);

    public CompleteComparisonLimits(IConfiguration configuration)
    {
        var configured = configuration["COMPARISON_MAX_REQUEST_BYTES"];
        if (configured is null) MaximumRequestBytes = DefaultMaximumRequestBytes;
        else if (int.TryParse(configured, NumberStyles.None, CultureInfo.InvariantCulture, out var bytes) && bytes is > 0 and < int.MaxValue)
            MaximumRequestBytes = bytes;
        else throw new InvalidOperationException("COMPARISON_MAX_REQUEST_BYTES must be a positive byte count below 2147483647.");
    }

    public void Dispose() => Slots.Dispose();
}
