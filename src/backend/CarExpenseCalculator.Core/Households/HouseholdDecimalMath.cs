using System.Numerics;

namespace CarExpenseCalculator.Core.Households;

internal static class HouseholdDecimalMath
{
    public static decimal Residual(decimal price, decimal annualPercent, int months)
    {
        if (annualPercent == 0m) return price;
        if (annualPercent == 100m) return 0m;
        var remaining = 1m - annualPercent / 100m;
        return price * (months % 12 == 0 ? Pow(remaining, months / 12) : Pow(TwelfthRoot(remaining), months));
    }

    private static decimal TwelfthRoot(decimal value)
    {
        var (coefficient, scale) = Parts(value);
        var low = 0m;
        var high = 1m;
        for (var step = 0; step < 96 && high - low > 0.000000000000000000000001m; step++)
        {
            var midpoint = low + (high - low) / 2m;
            if (midpoint == low || midpoint == high) break;
            var (candidate, candidateScale) = Parts(midpoint);
            // Compare exact base-ten integers. Decimal x^12 can underflow or
            // lose significant digits near 100% depreciation and choose a wrong root.
            var left = BigInteger.Pow(candidate, 12) * BigInteger.Pow(10, scale);
            var right = coefficient * BigInteger.Pow(10, candidateScale * 12);
            if (left == right) return midpoint;
            if (left < right) low = midpoint;
            else high = midpoint;
        }

        return low + (high - low) / 2m;
    }

    private static (BigInteger Coefficient, int Scale) Parts(decimal value)
    {
        var bits = decimal.GetBits(value);
        var coefficient = (BigInteger)(uint)bits[0] | ((BigInteger)(uint)bits[1] << 32) | ((BigInteger)(uint)bits[2] << 64);
        return (coefficient, (bits[3] >> 16) & 0xff);
    }

    private static decimal Pow(decimal value, int exponent)
    {
        var result = 1m;
        for (var i = 0; i < exponent; i++) result *= value;
        return result;
    }
}
