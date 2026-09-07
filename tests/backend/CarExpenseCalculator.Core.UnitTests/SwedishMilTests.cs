using System.Globalization;
using CarExpenseCalculator.Core.Comparisons;
using Xunit;

namespace CarExpenseCalculator.Core.UnitTests;

public sealed class SwedishMilTests
{
    [Theory]
    [InlineData("200000", "20000")]
    [InlineData("0", "0")]
    [InlineData("10000000", "1000000")]
    [InlineData("12345.678901234567890123456789", "1234.5678901234567890123456789")]
    [InlineData("0.000000000000000000000000001", "0.0000000000000000000000000001")]
    public void Conversion_preserves_exact_units_and_decimal_precision(string kilometres, string mil)
    {
        var kmValue = decimal.Parse(kilometres, CultureInfo.InvariantCulture);
        var milValue = decimal.Parse(mil, CultureInfo.InvariantCulture);
        Assert.Equal(milValue, SwedishMil.FromKilometres(kmValue));
        Assert.Equal(kmValue, SwedishMil.ToKilometres(milValue));
    }

    [Theory]
    [InlineData("0.0000000000000000000000000001")]
    [InlineData("4.0000000000000000000000000001")]
    public void Unrepresentable_division_is_not_silently_rounded(string kilometres)
    {
        var error = Assert.Single(Assert.Throws<VehicleFactsValidationException>(() =>
            SwedishMil.FromKilometres(decimal.Parse(kilometres, CultureInfo.InvariantCulture))).Errors);
        Assert.Equal("odometerKilometres", error.Path);
        Assert.Equal("conversionNotExact", error.Code);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("10000001")]
    [InlineData("79228162514264337593543950335")]
    public void Invalid_distances_produce_typed_errors_without_decimal_overflow(string value)
    {
        var number = decimal.Parse(value, CultureInfo.InvariantCulture);
        Assert.Equal("outOfRange", Assert.Single(Assert.Throws<VehicleFactsValidationException>(() =>
            SwedishMil.FromKilometres(number)).Errors).Code);
        Assert.Equal("outOfRange", Assert.Single(Assert.Throws<VehicleFactsValidationException>(() =>
            SwedishMil.ToKilometres(number)).Errors).Code);
    }
}
