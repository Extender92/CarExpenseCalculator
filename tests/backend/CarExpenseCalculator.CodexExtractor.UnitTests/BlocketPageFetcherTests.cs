using System.Net;
using System.Net.Http.Headers;
using System.Text;
using CarExpenseCalculator.Core.Listings;

namespace CarExpenseCalculator.CodexExtractor.UnitTests;

public sealed class BlocketPageFetcherTests
{
    private static readonly ListingUrl Url = ListingUrl.Parse("https://www.blocket.se/mobility/item/1?ci=3");

    [Theory]
    [InlineData("https://www.blocket.se/mobility/item/1?ci=3", true)]
    [InlineData("https://blocket.se/mobility/item/1", true)]
    [InlineData("http://www.blocket.se/mobility/item/1", false)]
    [InlineData("https://www.blocket.se:444/mobility/item/1", false)]
    [InlineData("https://www.blocket.se.evil.example/mobility/item/1", false)]
    [InlineData("https://www.blocket.se/mobility/search", false)]
    [InlineData("https://www.blocket.se/mobility/item/1/other", false)]
    public void Source_allowlist_is_exact(string value, bool expected) => Assert.Equal(expected, BlocketPageFetcher.Supports(ListingUrl.Parse(value)));

    [Theory]
    [InlineData("127.0.0.1", false)]
    [InlineData("10.0.0.1", false)]
    [InlineData("169.254.169.254", false)]
    [InlineData("::1", false)]
    [InlineData("::ffff:8.8.8.8", false)]
    [InlineData("fc00::1", false)]
    [InlineData("8.8.8.8", true)]
    public void Connection_addresses_use_the_existing_public_address_rules(string ip, bool expected) =>
        Assert.Equal(expected, BlocketPageFetcher.IsPublicAddress(IPAddress.Parse(ip)));

    [Theory]
    [InlineData(403, (int)CodexExecutionFailure.SourceBlocked)]
    [InlineData(404, (int)CodexExecutionFailure.SourceUnavailable)]
    [InlineData(410, (int)CodexExecutionFailure.SourceUnavailable)]
    [InlineData(500, (int)CodexExecutionFailure.SourceUnavailable)]
    public async Task Source_failures_are_typed_and_not_retried(int status, int expected)
    {
        using var handler = new Handler(_ => new((HttpStatusCode)status));
        using var client = new HttpClient(handler);
        var result = await Assert.ThrowsAsync<ListingSourceException>(() => new BlocketPageFetcher(client, TimeProvider.System).FetchAsync(Url, default));
        Assert.Equal((CodexExecutionFailure)expected, result.Failure);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Rate_limit_cooldown_prevents_a_second_source_request()
    {
        var clock = new Clock();
        using var handler = new Handler(_ => { var r = new HttpResponseMessage(HttpStatusCode.TooManyRequests); r.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(90)); return r; });
        using var client = new HttpClient(handler);
        var fetcher = new BlocketPageFetcher(client, clock);
        await Assert.ThrowsAsync<ListingSourceException>(() => fetcher.FetchAsync(Url, default));
        clock.Now = clock.Now.AddSeconds(20);
        var error = await Assert.ThrowsAsync<ListingSourceException>(() => fetcher.FetchAsync(Url, default));
        Assert.Equal(70, error.RetryAfterSeconds);
        Assert.Equal(1, handler.Calls);
        clock.Now = clock.Now.AddSeconds(70);
        await Assert.ThrowsAsync<ListingSourceException>(() => fetcher.FetchAsync(Url, default));
        Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData("https://evil.example/mobility/item/1")]
    [InlineData("https://www.blocket.se/mobility/item/2")]
    [InlineData("http://www.blocket.se/mobility/item/1")]
    public async Task Redirects_cannot_change_source_or_advertisement(string target)
    {
        using var handler = new Handler(_ => { var r = new HttpResponseMessage(HttpStatusCode.Redirect); r.Headers.Location = new(target); return r; });
        using var client = new HttpClient(handler);
        await Assert.ThrowsAsync<ListingSourceException>(() => new BlocketPageFetcher(client, TimeProvider.System).FetchAsync(Url, default));
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public async Task Actual_body_bytes_are_bounded_without_content_length(int extra, bool succeeds)
    {
        using var handler = new Handler(_ =>
        {
            var body = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(new string('å', BlocketPageFetcher.MaximumBytes / 2) + new string('x', extra))));
            body.Headers.ContentType = new("text/html");
            return new(HttpStatusCode.OK) { Content = body };
        });
        using var client = new HttpClient(handler);
        var task = new BlocketPageFetcher(client, TimeProvider.System).FetchAsync(Url, default);
        if (succeeds) Assert.Equal(BlocketPageFetcher.MaximumBytes / 2, (await task).Html.Length);
        else Assert.Equal(CodexExecutionFailure.SourceInvalidContent, (await Assert.ThrowsAsync<ListingSourceException>(() => task)).Failure);
    }

    [Fact]
    public async Task Cancellation_propagates_without_a_success_or_retry()
    {
        using var handler = new Handler(_ => throw new OperationCanceledException());
        using var client = new HttpClient(handler);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new BlocketPageFetcher(client, TimeProvider.System).FetchAsync(Url, cancellation.Token));
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Calls++; return Task.FromResult(send(request)); }
    }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.Parse("2026-09-10T12:00:00Z");
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
