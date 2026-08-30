using CarExpenseCalculator.Core.Vehicles;
using Xunit;

namespace CarExpenseCalculator.Core.UnitTests;

public sealed class RegistrationNumberTests
{
    [Theory]
    [InlineData("ABC123", "ABC123")]
    [InlineData("abc12d", "ABC12D")]
    [InlineData(" ABC-123 ", "ABC123")]
    [InlineData("A B C 1 2 D", "ABC12D")]
    public void Parse_normalizes_supported_ordinary_numbers(string input, string expected)
    {
        var registrationNumber = RegistrationNumber.Parse(input);

        Assert.Equal(expected, registrationNumber.Value);
        Assert.Equal(expected, registrationNumber.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("ABC12")]
    [InlineData("ABC1234")]
    [InlineData("1BC123")]
    [InlineData("ABC1D3")]
    [InlineData("ABI123")]
    [InlineData("ABQ123")]
    [InlineData("ABV123")]
    [InlineData("ABC12O")]
    [InlineData("MLB84Q")]
    [InlineData("AB123C")]
    [InlineData("PERSONLIG")]
    public void Parse_rejects_unsupported_or_invalid_numbers(string input)
    {
        Assert.Throws<RegistrationNumberValidationException>(() => RegistrationNumber.Parse(input));
        Assert.False(RegistrationNumber.TryParse(input, out var parsed));
        Assert.Null(parsed);
    }

    [Fact]
    public void Parse_rejects_null()
    {
        Assert.Throws<ArgumentNullException>(() => RegistrationNumber.Parse(null!));
        Assert.False(RegistrationNumber.TryParse(null, out var parsed));
        Assert.Null(parsed);
    }

    [Fact]
    public void Equal_normalized_numbers_have_value_equality()
    {
        Assert.Equal(
            RegistrationNumber.Parse("abc-123"),
            RegistrationNumber.Parse("ABC123"));
    }
}
