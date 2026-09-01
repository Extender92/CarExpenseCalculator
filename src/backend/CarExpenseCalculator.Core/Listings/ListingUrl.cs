using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace CarExpenseCalculator.Core.Listings;

public sealed class ListingUrl : IEquatable<ListingUrl>
{
    public const int MaximumLength = 2_048;

    private static readonly (uint Network, int PrefixLength)[] RejectedIpv4Ranges =
    [
        (ParseIpv4("0.0.0.0"), 8),
        (ParseIpv4("10.0.0.0"), 8),
        (ParseIpv4("100.64.0.0"), 10),
        (ParseIpv4("127.0.0.0"), 8),
        (ParseIpv4("169.254.0.0"), 16),
        (ParseIpv4("172.16.0.0"), 12),
        (ParseIpv4("192.0.0.0"), 24),
        (ParseIpv4("192.0.2.0"), 24),
        (ParseIpv4("192.88.99.0"), 24),
        (ParseIpv4("192.168.0.0"), 16),
        (ParseIpv4("198.18.0.0"), 15),
        (ParseIpv4("198.51.100.0"), 24),
        (ParseIpv4("203.0.113.0"), 24),
        (ParseIpv4("224.0.0.0"), 4),
        (ParseIpv4("240.0.0.0"), 4),
    ];

    private static readonly (byte[] Network, int PrefixLength)[] RejectedIpv6Ranges =
    [
        (ParseIpv6("::"), 128),
        (ParseIpv6("::1"), 128),
        (ParseIpv6("64:ff9b::"), 96),
        (ParseIpv6("64:ff9b:1::"), 48),
        (ParseIpv6("100::"), 64),
        (ParseIpv6("2001::"), 23),
        (ParseIpv6("2001:db8::"), 32),
        (ParseIpv6("2002::"), 16),
        (ParseIpv6("2620:4f:8000::"), 48),
        (ParseIpv6("3fff::"), 20),
        (ParseIpv6("5f00::"), 16),
        (ParseIpv6("fc00::"), 7),
        (ParseIpv6("fec0::"), 10),
        (ParseIpv6("fe80::"), 10),
        (ParseIpv6("ff00::"), 8),
    ];

    private ListingUrl(
        string submittedValue,
        string value,
        string scheme,
        string host,
        int? nonDefaultPort,
        string escapedPath,
        string query)
    {
        SubmittedValue = submittedValue;
        Value = value;
        Scheme = scheme;
        Host = host;
        NonDefaultPort = nonDefaultPort;
        EscapedPath = escapedPath;
        Query = query;
    }

    public string SubmittedValue { get; }

    public string Value { get; }

    public string Scheme { get; }

    public string Host { get; }

    public int? NonDefaultPort { get; }

    public string EscapedPath { get; }

    public string Query { get; }

    public static ListingUrl Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            throw Invalid(
                ListingUrlValidationErrorCode.Required,
                "URL must contain at least one non-whitespace character.",
                value);
        }

        if (trimmed.Length > MaximumLength)
        {
            throw Invalid(
                ListingUrlValidationErrorCode.TooLong,
                $"URL cannot exceed {MaximumLength} characters.",
                value);
        }

        if (trimmed.IndexOf(':', StringComparison.Ordinal) <= 0
            || !Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            throw Invalid(ListingUrlValidationErrorCode.Malformed, "URL must be absolute and well formed.", value);
        }

        var scheme = uri.Scheme.ToLowerInvariant();
        if (scheme != Uri.UriSchemeHttp && scheme != Uri.UriSchemeHttps)
        {
            throw Invalid(
                ListingUrlValidationErrorCode.UnsupportedScheme,
                "URL scheme must be HTTP or HTTPS.",
                value);
        }

        if (uri.UserInfo.Length > 0)
        {
            throw Invalid(
                ListingUrlValidationErrorCode.CredentialsNotAllowed,
                "URL credentials are not allowed.",
                value);
        }

        if (uri.Host.Length == 0)
        {
            throw Invalid(ListingUrlValidationErrorCode.MissingHost, "URL must contain a host.", value);
        }

        string host;
        if (uri.HostNameType == UriHostNameType.IPv6)
        {
            host = uri.Host.Trim('[', ']').ToLowerInvariant();
        }
        else
        {
            try
            {
                host = new IdnMapping().GetAscii(uri.Host).ToLowerInvariant();
            }
            catch (ArgumentException)
            {
                throw Invalid(ListingUrlValidationErrorCode.Malformed, "URL host is not valid.", value);
            }
        }

        var classificationHost = host.TrimEnd('.');
        if (classificationHost.Length == 0)
        {
            throw Invalid(ListingUrlValidationErrorCode.MissingHost, "URL must contain a host.", value);
        }

        if (classificationHost.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || classificationHost.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || classificationHost.Equals("local", StringComparison.OrdinalIgnoreCase)
            || classificationHost.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
        {
            throw Invalid(
                ListingUrlValidationErrorCode.LocalHostNotAllowed,
                "Local host names are not allowed.",
                value);
        }

        IPAddress? ipAddress = null;
        if (IPAddress.TryParse(classificationHost, out var parsedAddress))
        {
            ipAddress = parsedAddress;
            if (IsRejectedAddress(parsedAddress))
            {
                throw Invalid(
                    ListingUrlValidationErrorCode.NonPublicIpAddress,
                    "Private, reserved, and other non-public IP addresses are not allowed.",
                    value);
            }
        }

        if (uri.Port is <= 0 or > 65_535)
        {
            throw Invalid(ListingUrlValidationErrorCode.InvalidPort, "URL port must be between 1 and 65535.", value);
        }

        int? nonDefaultPort = uri.IsDefaultPort ? null : uri.Port;
        var escapedPath = uri.GetComponents(UriComponents.Path, UriFormat.UriEscaped);
        escapedPath = escapedPath.Length == 0 ? "/" : $"/{escapedPath.TrimStart('/')}";
        var query = uri.GetComponents(UriComponents.Query, UriFormat.UriEscaped);
        query = query.Length == 0 ? string.Empty : $"?{query}";
        var authorityHost = ipAddress?.AddressFamily == AddressFamily.InterNetworkV6
            ? $"[{host}]"
            : host;
        var portText = nonDefaultPort is null ? string.Empty : $":{nonDefaultPort.Value}";
        var normalized = $"{scheme}://{authorityHost}{portText}{escapedPath}{query}";

        if (normalized.Length > MaximumLength)
        {
            throw Invalid(
                ListingUrlValidationErrorCode.TooLong,
                $"Normalized URL cannot exceed {MaximumLength} characters.",
                value);
        }

        return new ListingUrl(trimmed, normalized, scheme, host, nonDefaultPort, escapedPath, query);
    }

    public static bool TryParse(string? value, out ListingUrl? listingUrl)
    {
        if (value is null)
        {
            listingUrl = null;
            return false;
        }

        try
        {
            listingUrl = Parse(value);
            return true;
        }
        catch (ListingUrlValidationException)
        {
            listingUrl = null;
            return false;
        }
    }

    public bool HasSamePageIdentity(ListingUrl other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (!Host.Equals(other.Host, StringComparison.Ordinal)
            || !GetComparablePath().Equals(other.GetComparablePath(), StringComparison.Ordinal))
        {
            return false;
        }

        if (NonDefaultPort is not null || other.NonDefaultPort is not null)
        {
            return Scheme.Equals(other.Scheme, StringComparison.Ordinal)
                && NonDefaultPort == other.NonDefaultPort;
        }

        return true;
    }

    public bool IsSourceMatchFor(ListingUrl submittedUrl)
    {
        ArgumentNullException.ThrowIfNull(submittedUrl);

        if (!Host.Equals(submittedUrl.Host, StringComparison.Ordinal)
            || !GetComparablePath().Equals(submittedUrl.GetComparablePath(), StringComparison.Ordinal))
        {
            return false;
        }

        if (NonDefaultPort is not null || submittedUrl.NonDefaultPort is not null)
        {
            return Scheme.Equals(submittedUrl.Scheme, StringComparison.Ordinal)
                && NonDefaultPort == submittedUrl.NonDefaultPort;
        }

        return Scheme.Equals(submittedUrl.Scheme, StringComparison.Ordinal)
            || (submittedUrl.Scheme == Uri.UriSchemeHttp && Scheme == Uri.UriSchemeHttps);
    }

    public bool Equals(ListingUrl? other)
    {
        return other is not null && Value.Equals(other.Value, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => Equals(obj as ListingUrl);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;

    private static ListingUrlValidationException Invalid(
        ListingUrlValidationErrorCode code,
        string message,
        string? value)
    {
        return new ListingUrlValidationException(code, message, value);
    }

    private string GetComparablePath()
    {
        return EscapedPath.Length > 1 && EscapedPath.EndsWith("/", StringComparison.Ordinal)
            ? EscapedPath[..^1]
            : EscapedPath;
    }

    private static bool IsRejectedAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var value = ToIpv4UInt32(address.GetAddressBytes());
            return RejectedIpv4Ranges.Any(range => IsIpv4InRange(value, range.Network, range.PrefixLength));
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return true;
        }

        var bytes = address.GetAddressBytes();
        return RejectedIpv6Ranges.Any(range => IsInRange(bytes, range.Network, range.PrefixLength));
    }

    private static bool IsIpv4InRange(uint address, uint network, int prefixLength)
    {
        var mask = prefixLength == 0 ? 0u : uint.MaxValue << (32 - prefixLength);
        return (address & mask) == (network & mask);
    }

    private static bool IsInRange(byte[] address, byte[] network, int prefixLength)
    {
        var fullBytes = prefixLength / 8;
        var remainingBits = prefixLength % 8;
        for (var index = 0; index < fullBytes; index++)
        {
            if (address[index] != network[index])
            {
                return false;
            }
        }

        if (remainingBits == 0)
        {
            return true;
        }

        var mask = (byte)(0xff << (8 - remainingBits));
        return (address[fullBytes] & mask) == (network[fullBytes] & mask);
    }

    private static uint ParseIpv4(string value)
    {
        return ToIpv4UInt32(IPAddress.Parse(value).GetAddressBytes());
    }

    private static uint ToIpv4UInt32(byte[] bytes)
    {
        return ((uint)bytes[0] << 24)
            | ((uint)bytes[1] << 16)
            | ((uint)bytes[2] << 8)
            | bytes[3];
    }

    private static byte[] ParseIpv6(string value) => IPAddress.Parse(value).GetAddressBytes();
}
