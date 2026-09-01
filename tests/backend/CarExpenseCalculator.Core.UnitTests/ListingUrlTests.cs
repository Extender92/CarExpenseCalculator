using System.Net;
using CarExpenseCalculator.Core.Listings;
using Xunit;

namespace CarExpenseCalculator.Core.UnitTests;

public sealed class ListingUrlTests
{
    public static TheoryData<string, int> DocumentedIpv4Ranges => new()
    {
        { "0.0.0.0", 8 },
        { "10.0.0.0", 8 },
        { "100.64.0.0", 10 },
        { "127.0.0.0", 8 },
        { "169.254.0.0", 16 },
        { "172.16.0.0", 12 },
        { "192.0.0.0", 24 },
        { "192.0.2.0", 24 },
        { "192.88.99.0", 24 },
        { "192.168.0.0", 16 },
        { "198.18.0.0", 15 },
        { "198.51.100.0", 24 },
        { "203.0.113.0", 24 },
        { "224.0.0.0", 4 },
        { "240.0.0.0", 4 },
    };

    public static TheoryData<string, int> DocumentedIpv6Ranges => new()
    {
        { "::", 128 },
        { "::1", 128 },
        { "64:ff9b::", 96 },
        { "64:ff9b:1::", 48 },
        { "100::", 64 },
        { "2001::", 23 },
        { "2001:db8::", 32 },
        { "2002::", 16 },
        { "2620:4f:8000::", 48 },
        { "3fff::", 20 },
        { "5f00::", 16 },
        { "fc00::", 7 },
        { "fec0::", 10 },
        { "fe80::", 10 },
        { "ff00::", 8 },
    };

    [Fact]
    public void Parse_normalizes_url_without_losing_path_or_query_information()
    {
        var result = ListingUrl.Parse(
            "  HTTPS://BÜCHER.Example:443/Vehicle%2FAbC?B=Two&a=One#details  ");

        Assert.Equal(
            "https://xn--bcher-kva.example/Vehicle%2FAbC?B=Two&a=One",
            result.Value);
        Assert.Equal("xn--bcher-kva.example", result.Host);
        Assert.Equal("/Vehicle%2FAbC", result.EscapedPath);
        Assert.Equal("?B=Two&a=One", result.Query);
        Assert.Null(result.NonDefaultPort);
    }

    [Theory]
    [InlineData("http://example.com", "http://example.com/")]
    [InlineData("http://example.com:80/path", "http://example.com/path")]
    [InlineData("https://example.com:443/path", "https://example.com/path")]
    [InlineData("https://example.com:8443/path", "https://example.com:8443/path")]
    public void Parse_normalizes_paths_and_default_ports(string input, string expected)
    {
        Assert.Equal(expected, ListingUrl.Parse(input).Value);
    }

    [Fact]
    public void Parse_accepts_public_ip_literals_and_unresolved_hostnames_without_dns()
    {
        Assert.Equal("https://8.8.8.8/", ListingUrl.Parse("https://8.8.8.8").Value);
        Assert.Equal(
            "https://[2606:4700:4700::1111]/",
            ListingUrl.Parse("https://[2606:4700:4700::1111]").Value);
        Assert.Equal(
            "https://does-not-exist.example/vehicle",
            ListingUrl.Parse("https://does-not-exist.example/vehicle").Value);
    }

    [Theory]
    [InlineData("", ListingUrlValidationErrorCode.Required)]
    [InlineData("   ", ListingUrlValidationErrorCode.Required)]
    [InlineData("/relative", ListingUrlValidationErrorCode.Malformed)]
    [InlineData("ftp://example.com/car", ListingUrlValidationErrorCode.UnsupportedScheme)]
    [InlineData("https://user:secret@example.com/car", ListingUrlValidationErrorCode.CredentialsNotAllowed)]
    [InlineData("https://localhost/car", ListingUrlValidationErrorCode.LocalHostNotAllowed)]
    [InlineData("https://cars.localhost/car", ListingUrlValidationErrorCode.LocalHostNotAllowed)]
    [InlineData("https://server.local/car", ListingUrlValidationErrorCode.LocalHostNotAllowed)]
    [InlineData("https://server.local./car", ListingUrlValidationErrorCode.LocalHostNotAllowed)]
    [InlineData("https://example.com:0/car", ListingUrlValidationErrorCode.InvalidPort)]
    [InlineData("https://example.com:65536/car", ListingUrlValidationErrorCode.Malformed)]
    [InlineData("https://example.com:not-a-port/car", ListingUrlValidationErrorCode.Malformed)]
    public void Parse_returns_stable_error_codes(string input, ListingUrlValidationErrorCode expectedCode)
    {
        var exception = Assert.Throws<ListingUrlValidationException>(() => ListingUrl.Parse(input));

        Assert.Equal(expectedCode, exception.Code);
        Assert.False(ListingUrl.TryParse(input, out var parsed));
        Assert.Null(parsed);
    }

    [Theory]
    [InlineData("0.0.0.1")]
    [InlineData("10.0.0.1")]
    [InlineData("100.64.0.1")]
    [InlineData("127.0.0.1")]
    [InlineData("169.254.1.1")]
    [InlineData("172.16.0.1")]
    [InlineData("192.0.0.1")]
    [InlineData("192.0.2.1")]
    [InlineData("192.88.99.1")]
    [InlineData("192.168.1.1")]
    [InlineData("198.18.0.1")]
    [InlineData("198.51.100.1")]
    [InlineData("203.0.113.1")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    public void Parse_rejects_documented_ipv4_ranges(string address)
    {
        var exception = Assert.Throws<ListingUrlValidationException>(
            () => ListingUrl.Parse($"https://{address}/car"));

        Assert.Equal(ListingUrlValidationErrorCode.NonPublicIpAddress, exception.Code);
    }

    [Theory]
    [InlineData("::")]
    [InlineData("::1")]
    [InlineData("::ffff:0.0.0.0")]
    [InlineData("::ffff:192.168.1.1")]
    [InlineData("::ffff:255.255.255.255")]
    [InlineData("64:ff9b::1")]
    [InlineData("64:ff9b:1::1")]
    [InlineData("100::1")]
    [InlineData("2001::1")]
    [InlineData("2001:db8::1")]
    [InlineData("2002::1")]
    [InlineData("2620:4f:8000::1")]
    [InlineData("3fff::1")]
    [InlineData("5f00::1")]
    [InlineData("fc00::1")]
    [InlineData("fec0::1")]
    [InlineData("fe80::1")]
    [InlineData("ff00::1")]
    public void Parse_rejects_documented_ipv6_ranges(string address)
    {
        var exception = Assert.Throws<ListingUrlValidationException>(
            () => ListingUrl.Parse($"https://[{address}]/car"));

        Assert.Equal(ListingUrlValidationErrorCode.NonPublicIpAddress, exception.Code);
    }

    [Theory]
    [MemberData(nameof(DocumentedIpv4Ranges))]
    public void Parse_rejects_first_and_last_address_of_each_ipv4_range(string network, int prefixLength)
    {
        var bytes = IPAddress.Parse(network).GetAddressBytes();
        var value = ((uint)bytes[0] << 24)
            | ((uint)bytes[1] << 16)
            | ((uint)bytes[2] << 8)
            | bytes[3];
        var mask = prefixLength == 0 ? 0u : uint.MaxValue << (32 - prefixLength);
        var first = value & mask;
        var last = first | ~mask;

        AssertRejectedIpv4(first);
        AssertRejectedIpv4(last);
    }

    [Theory]
    [MemberData(nameof(DocumentedIpv6Ranges))]
    public void Parse_rejects_first_and_last_address_of_each_ipv6_range(string network, int prefixLength)
    {
        var first = IPAddress.Parse(network).GetAddressBytes();
        var last = first.ToArray();
        var fullBytes = prefixLength / 8;
        var remainingBits = prefixLength % 8;
        if (remainingBits > 0)
        {
            var mask = (byte)(0xff << (8 - remainingBits));
            first[fullBytes] &= mask;
            last[fullBytes] = (byte)(first[fullBytes] | ~mask);
            fullBytes++;
        }

        for (var index = fullBytes; index < last.Length; index++)
        {
            first[index] = 0;
            last[index] = 0xff;
        }

        AssertRejectedIpv6(new IPAddress(first));
        AssertRejectedIpv6(new IPAddress(last));
    }

    [Fact]
    public void Parse_enforces_submitted_length_boundary()
    {
        const string prefix = "https://example.com/";
        var accepted = prefix + new string('a', ListingUrl.MaximumLength - prefix.Length);
        var rejected = accepted + "a";

        Assert.Equal(ListingUrl.MaximumLength, ListingUrl.Parse(accepted).Value.Length);
        Assert.Equal(
            ListingUrlValidationErrorCode.TooLong,
            Assert.Throws<ListingUrlValidationException>(() => ListingUrl.Parse(rejected)).Code);
    }

    [Fact]
    public void Parse_rejects_null_and_try_parse_returns_false()
    {
        Assert.Throws<ArgumentNullException>(() => ListingUrl.Parse(null!));
        Assert.False(ListingUrl.TryParse(null, out var parsed));
        Assert.Null(parsed);
    }

    [Fact]
    public void Equality_uses_complete_normalized_url()
    {
        Assert.Equal(
            ListingUrl.Parse("HTTPS://EXAMPLE.COM/car?a=1"),
            ListingUrl.Parse("https://example.com:443/car?a=1#fragment"));
        Assert.NotEqual(
            ListingUrl.Parse("https://example.com/car?a=1"),
            ListingUrl.Parse("https://example.com/car?a=2"));
    }

    [Theory]
    [InlineData("https://example.com/car?first=1", "https://example.com/car?second=2", true)]
    [InlineData("https://example.com/car", "https://example.com/car/", true)]
    [InlineData("http://example.com/car", "https://example.com/car", true)]
    [InlineData("http://example.com:8080/car", "https://example.com:8080/car", false)]
    [InlineData("https://example.com/Car", "https://example.com/car", false)]
    [InlineData("https://other.example/car", "https://example.com/car", false)]
    public void Page_identity_has_documented_equivalence(
        string first,
        string second,
        bool expected)
    {
        Assert.Equal(
            expected,
            ListingUrl.Parse(first).HasSamePageIdentity(ListingUrl.Parse(second)));
    }

    [Theory]
    [InlineData("http://example.com/car", "https://example.com/car", true)]
    [InlineData("https://example.com/car", "http://example.com/car", false)]
    [InlineData("https://example.com/car?source=result", "https://example.com/car?source=input", true)]
    [InlineData("https://example.com/car/", "https://example.com/car", true)]
    [InlineData("https://example.com/car", "https://example.com/other", false)]
    [InlineData("https://example.com:8443/car", "https://example.com:8443/car", true)]
    [InlineData("https://example.com:8443/car", "https://example.com:9443/car", false)]
    public void Source_matching_is_directional(
        string submitted,
        string returnedSource,
        bool expected)
    {
        Assert.Equal(
            expected,
            ListingUrl.Parse(returnedSource).IsSourceMatchFor(ListingUrl.Parse(submitted)));
    }

    private static void AssertRejectedIpv4(uint value)
    {
        var address = new IPAddress(
        [
            (byte)(value >> 24),
            (byte)(value >> 16),
            (byte)(value >> 8),
            (byte)value,
        ]);
        var exception = Assert.Throws<ListingUrlValidationException>(
            () => ListingUrl.Parse($"https://{address}/car"));
        Assert.Equal(ListingUrlValidationErrorCode.NonPublicIpAddress, exception.Code);
    }

    private static void AssertRejectedIpv6(IPAddress address)
    {
        var exception = Assert.Throws<ListingUrlValidationException>(
            () => ListingUrl.Parse($"https://[{address}]/car"));
        Assert.Equal(ListingUrlValidationErrorCode.NonPublicIpAddress, exception.Code);
    }
}
