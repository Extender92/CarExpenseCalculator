using CarExpenseCalculator.Core.Listings;

namespace CarExpenseCalculator.Api.Mapping;

internal static class ListingUrlValidationMessages
{
    public static string Get(ListingUrlValidationErrorCode code)
    {
        return code switch
        {
            ListingUrlValidationErrorCode.Required => "URL is required.",
            ListingUrlValidationErrorCode.TooLong => "URL cannot exceed 2048 characters.",
            ListingUrlValidationErrorCode.Malformed =>
                "URL must be an absolute, well-formed HTTP or HTTPS URL.",
            ListingUrlValidationErrorCode.UnsupportedScheme => "URL scheme must be HTTP or HTTPS.",
            ListingUrlValidationErrorCode.CredentialsNotAllowed => "URL credentials are not allowed.",
            ListingUrlValidationErrorCode.MissingHost => "URL must contain a host.",
            ListingUrlValidationErrorCode.LocalHostNotAllowed => "Local host names are not allowed.",
            ListingUrlValidationErrorCode.NonPublicIpAddress =>
                "Private, reserved, and other non-public IP addresses are not allowed.",
            ListingUrlValidationErrorCode.InvalidPort => "URL port must be between 1 and 65535.",
            _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unsupported URL validation error."),
        };
    }
}
