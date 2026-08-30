namespace CarExpenseCalculator.Core.Vehicles;

public sealed record RegistrationNumber
{
    private const int NormalizedLength = 6;
    private const string OrdinaryLetters = "ABCDEFGHJKLMNOPRSTUWXYZ";
    private const string FinalLetters = "ABCDEFGHJKLMNPRSTUWXYZ";

    private RegistrationNumber(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static RegistrationNumber Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var normalized = Normalize(value);
        if (!IsOrdinarySwedishRegistrationNumber(normalized))
        {
            throw new RegistrationNumberValidationException(value);
        }

        return new RegistrationNumber(normalized);
    }

    public static bool TryParse(string? value, out RegistrationNumber? registrationNumber)
    {
        if (value is null)
        {
            registrationNumber = null;
            return false;
        }

        var normalized = Normalize(value);
        if (!IsOrdinarySwedishRegistrationNumber(normalized))
        {
            registrationNumber = null;
            return false;
        }

        registrationNumber = new RegistrationNumber(normalized);
        return true;
    }

    public override string ToString() => Value;

    private static string Normalize(string value)
    {
        return string.Concat(value
            .Where(character => character != '-' && !char.IsWhiteSpace(character))
            .Select(char.ToUpperInvariant));
    }

    private static bool IsOrdinarySwedishRegistrationNumber(string value)
    {
        return value.Length == NormalizedLength
            && OrdinaryLetters.Contains(value[0], StringComparison.Ordinal)
            && OrdinaryLetters.Contains(value[1], StringComparison.Ordinal)
            && OrdinaryLetters.Contains(value[2], StringComparison.Ordinal)
            && char.IsAsciiDigit(value[3])
            && char.IsAsciiDigit(value[4])
            && (char.IsAsciiDigit(value[5])
                || FinalLetters.Contains(value[5], StringComparison.Ordinal));
    }
}

public sealed class RegistrationNumberValidationException : ArgumentException
{
    public RegistrationNumberValidationException(string value)
        : base(
            $"'{value}' is not a supported ordinary Swedish registration number.",
            nameof(value))
    {
    }
}
