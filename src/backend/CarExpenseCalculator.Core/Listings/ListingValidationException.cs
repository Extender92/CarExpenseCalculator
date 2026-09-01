namespace CarExpenseCalculator.Core.Listings;

public sealed record ListingValidationError(
    string Path,
    string Message);

public sealed class ListingValidationException : Exception
{
    public ListingValidationException(IEnumerable<ListingValidationError> errors)
        : base("The listing input is invalid.")
    {
        ArgumentNullException.ThrowIfNull(errors);
        Errors = Array.AsReadOnly(errors.ToArray());
    }

    public IReadOnlyList<ListingValidationError> Errors { get; }
}

public enum ListingUrlValidationErrorCode
{
    Required,
    TooLong,
    Malformed,
    UnsupportedScheme,
    CredentialsNotAllowed,
    MissingHost,
    LocalHostNotAllowed,
    NonPublicIpAddress,
    InvalidPort,
}

public sealed class ListingUrlValidationException : ArgumentException
{
    public ListingUrlValidationException(
        ListingUrlValidationErrorCode code,
        string message,
        string? value)
        : base(message, nameof(value))
    {
        Code = code;
        RejectedValue = value;
    }

    public ListingUrlValidationErrorCode Code { get; }

    public string? RejectedValue { get; }
}
