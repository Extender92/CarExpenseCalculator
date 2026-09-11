using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using CarExpenseCalculator.Core.Listings;

namespace CarExpenseCalculator.CodexExtractor;

internal sealed record RetrievedPage(ListingUrl Url, string Html);

internal interface IListingPageFetcher
{
    Task<RetrievedPage> FetchAsync(ListingUrl url, CancellationToken cancellationToken);
}

internal sealed class ListingSourceException(CodexExecutionFailure failure, int? retryAfterSeconds = null) : Exception
{
    public CodexExecutionFailure Failure { get; } = failure;
    public int? RetryAfterSeconds { get; } = retryAfterSeconds;
}

internal sealed partial class BlocketPageFetcher(HttpClient client, TimeProvider clock) : IListingPageFetcher, IDisposable
{
    internal const int MaximumBytes = 10 * 1024 * 1024;
    private DateTimeOffset retryAt;

    public void Dispose() => client.Dispose();

    internal static bool Supports(ListingUrl url) => url.Scheme == "https" && url.NonDefaultPort is null &&
        url.Host is "blocket.se" or "www.blocket.se" && ListingPath().IsMatch(url.EscapedPath);

    public async Task<RetrievedPage> FetchAsync(ListingUrl url, CancellationToken cancellationToken)
    {
        if (!Supports(url)) throw new ListingSourceException(CodexExecutionFailure.SourceUnsupported);
        if (retryAt > clock.GetUtcNow()) throw new ListingSourceException(CodexExecutionFailure.SourceRateLimited, RemainingRetry());
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var current = url;
        try
        {
            for (var redirects = 0; ; redirects++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, current.Value);
                request.Headers.UserAgent.ParseAdd("CarExpenseCalculator/0.1 (user-requested listing retrieval)");
                request.Headers.Accept.ParseAdd("text/html");
                request.Headers.AcceptLanguage.ParseAdd("sv-SE,sv;q=0.9");
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var retry = response.Headers.RetryAfter;
                    var now = clock.GetUtcNow();
                    var delay = retry?.Delta ?? (retry?.Date - now) ?? TimeSpan.FromSeconds(60);
                    if (delay <= TimeSpan.Zero) delay = TimeSpan.FromSeconds(60);
                    delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds, int.MaxValue));
                    retryAt = now.Add(delay);
                    throw new ListingSourceException(CodexExecutionFailure.SourceRateLimited, RemainingRetry());
                }
                if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
                    throw new ListingSourceException(CodexExecutionFailure.SourceBlocked);
                if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or
                    HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
                {
                    if (redirects >= 3 || response.Headers.Location is null ||
                        !Uri.TryCreate(new Uri(current.Value), response.Headers.Location, out var target) ||
                        !ListingUrl.TryParse(target.AbsoluteUri, out var next) || !Supports(next!) ||
                        next!.EscapedPath != url.EscapedPath)
                        throw new ListingSourceException(CodexExecutionFailure.SourceInvalidContent);
                    current = next;
                    continue;
                }
                if (response.StatusCode != HttpStatusCode.OK)
                    throw new ListingSourceException(CodexExecutionFailure.SourceUnavailable);
                if (!string.Equals(response.Content.Headers.ContentType?.MediaType, "text/html", StringComparison.OrdinalIgnoreCase))
                    throw new ListingSourceException(CodexExecutionFailure.SourceInvalidContent);
                var charset = response.Content.Headers.ContentType?.CharSet?.Trim('"');
                if (charset is not null && !charset.Equals("utf-8", StringComparison.OrdinalIgnoreCase))
                    throw new ListingSourceException(CodexExecutionFailure.SourceInvalidContent);
                await using var body = await response.Content.ReadAsStreamAsync(timeout.Token);
                using var buffer = new MemoryStream();
                var chunk = new byte[16 * 1024];
                int count;
                while ((count = await body.ReadAsync(chunk, timeout.Token)) != 0)
                {
                    if (buffer.Length + count > MaximumBytes)
                        throw new ListingSourceException(CodexExecutionFailure.SourceInvalidContent);
                    buffer.Write(chunk, 0, count);
                }
                timeout.Token.ThrowIfCancellationRequested();
                return new(current, new UTF8Encoding(false, true).GetString(buffer.GetBuffer(), 0, (int)buffer.Length));
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new ListingSourceException(CodexExecutionFailure.TimedOut); }
        catch (DecoderFallbackException)
        { throw new ListingSourceException(CodexExecutionFailure.SourceInvalidContent); }
        catch (Exception error) when (error is HttpRequestException or IOException)
        { throw new ListingSourceException(CodexExecutionFailure.SourceUnavailable); }
    }

    private int RemainingRetry() => (int)Math.Clamp(Math.Ceiling((retryAt - clock.GetUtcNow()).TotalSeconds), 1, int.MaxValue);

    internal static bool IsPublicAddress(IPAddress address) => ListingUrl.TryParse(
        address.AddressFamily == AddressFamily.InterNetworkV6 ? $"https://[{address}]/" : $"https://{address}/", out _);

    internal static SocketsHttpHandler CreateHandler() => new()
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        UseProxy = false,
        AutomaticDecompression = DecompressionMethods.All,
        ConnectTimeout = TimeSpan.FromSeconds(10),
        ConnectCallback = async (context, token) =>
        {
            if (context.DnsEndPoint.Host is not ("blocket.se" or "www.blocket.se") || context.DnsEndPoint.Port != 443)
                throw new HttpRequestException("Unsupported listing destination.");
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, token);
            if (addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address)))
                throw new HttpRequestException("Non-public listing destination.");
            // Connect to a validated literal address; do not resolve the name a second time.
            foreach (var address in addresses)
            {
                var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(address, 443), token);
                    if (socket.RemoteEndPoint is not IPEndPoint remote || !remote.Address.Equals(address) || !IsPublicAddress(remote.Address))
                        throw new HttpRequestException("Unexpected listing destination.");
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch (SocketException) { socket.Dispose(); }
                catch { socket.Dispose(); throw; }
            }
            throw new HttpRequestException("Listing connection failed.");
        },
    };

    [GeneratedRegex(@"^/mobility/item/[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex ListingPath();
}
