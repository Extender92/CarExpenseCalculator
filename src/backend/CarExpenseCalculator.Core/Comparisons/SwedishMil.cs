namespace CarExpenseCalculator.Core.Comparisons;

public static class SwedishMil
{
    public static decimal FromKilometres(decimal kilometres) => Convert(kilometres, toMil: true);
    public static decimal ToKilometres(decimal mil) => Convert(mil, toMil: false);

    private static decimal Convert(decimal value, bool toMil)
    {
        var path = toMil ? "odometerKilometres" : "odometerMil";
        var maximum = VehicleFactLimits.MaximumOdometerKilometres / (toMil ? 1m : 10m);
        if (value < 0 || value > maximum)
        {
            throw Error(path, "outOfRange", "Odometer is outside the supported distance range.");
        }

        var result = toMil ? value / 10m : value * 10m;
        var restored = toMil ? result * 10m : result / 10m;
        if (restored != value)
        {
            throw Error(path, "conversionNotExact", "Distance cannot be represented exactly in the target decimal unit.");
        }

        return result;
    }

    private static VehicleFactsValidationException Error(string path, string code, string message) =>
        new([new(path, code, message)]);
}
