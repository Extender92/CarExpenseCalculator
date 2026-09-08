using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CarExpenseCalculator.Infrastructure.Persistence.Comparisons;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using static CarExpenseCalculator.Api.IntegrationTests.HouseholdApiTestData;
using static CarExpenseCalculator.Api.IntegrationTests.CompleteComparisonApiData;

namespace CarExpenseCalculator.Api.IntegrationTests;

public sealed class CompleteComparisonCancellationTests
{
    private const string Token = "v1:0000000000000000000000000000000000000000000000000000000000000000";
    private static JsonObject Request() => Stored(new JsonObject { ["baselineToken"] = Token, ["householdProfileRevision"] = 0, ["ruleProfileRevision"] = 0 });

    [Fact]
    public async Task A_missing_snapshot_group_cannot_be_published_as_a_complete_comparison()
    {
        var store = new ControlledSnapshot { MissingGroup = true }; using var factory = new ManualCalculationApiFactory();
        using var configured = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        { s.RemoveAll<IComparisonSnapshotStore>(); s.AddSingleton<IComparisonSnapshotStore>(store); }));
        using var client = configured.CreateClient(); store.Release.TrySetResult();
        var error = await Json(await client.PostAsJsonAsync(Route, Request()), HttpStatusCode.ServiceUnavailable);
        Assert.Equal("comparisonStorageUnavailable", error["code"]!.GetValue<string>());
        Assert.Null(error["views"]);
    }

    [Fact]
    public async Task Cancelling_an_unfinished_upload_releases_the_Kestrel_request_slot()
    {
        using var factory = new ManualCalculationApiFactory(); factory.UseKestrel(0);
        using var client = factory.CreateClient();
        var limits = factory.Services.GetRequiredService<Api.Comparisons.CompleteComparisonLimits>();
        using var socket = new System.Net.Sockets.TcpClient();
        await socket.ConnectAsync(client.BaseAddress!.Host, client.BaseAddress.Port);
        try
        {
            var prefix = System.Text.Encoding.ASCII.GetBytes($"POST {Route} HTTP/1.1\r\nHost: localhost\r\nContent-Type: application/json\r\nTransfer-Encoding: chunked\r\n\r\n1\r\n{{\r\n");
            await socket.GetStream().WriteAsync(prefix);
            await WaitForSlots(limits, 1);
        }
        finally { socket.Dispose(); }
        await WaitForSlots(limits, 2);
        await Json(await client.PostAsJsonAsync(Route, Manual(0)));
    }

    [Fact]
    public async Task Disconnected_large_response_releases_the_slot_without_a_complete_JSON_result()
    {
        using var factory = new ManualCalculationApiFactory(); factory.UseKestrel(0);
        using var client = factory.CreateClient(); using var cancel = new CancellationTokenSource();
        var limits = factory.Services.GetRequiredService<Api.Comparisons.CompleteComparisonLimits>();
        var body = Manual(101);
        foreach (var car in body["candidates"]!.AsArray())
        {
            var cost = Vehicle();
            cost["customCosts"]!["items"] = new JsonArray(Enumerable.Range(0, 50).Select(i => (JsonNode)new JsonObject
            {
                ["key"] = $"item-{i}", ["label"] = "Estimate", ["evidenceNote"] = new string('x', 1000),
                ["cadence"] = "once", ["monthOffset"] = 1, ["amountSek"] = new JsonObject { ["single"] = 1 },
            }).ToArray());
            car!["costInput"] = cost;
        }
        using var request = new HttpRequestMessage(HttpMethod.Post, Route) { Content = JsonContent.Create(body) };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stream = await response.Content.ReadAsStreamAsync(cancel.Token);
        var prefix = new byte[1];
        Assert.Equal(1, await stream.ReadAsync(prefix, cancel.Token));
        Assert.Equal((byte)'{', prefix[0]);
        cancel.Cancel(); response.Dispose();
        await WaitForSlots(limits, 2);
        await Json(await client.PostAsJsonAsync(Route, Manual(0)));
    }

    private static async Task WaitForSlots(Api.Comparisons.CompleteComparisonLimits limits, int expected)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (limits.Slots.CurrentCount != expected) await Task.Delay(10, deadline.Token);
    }

    [Fact]
    public async Task Two_slots_reject_a_third_request_and_cancellation_releases_a_slot()
    {
        var store = new ControlledSnapshot(); using var factory = new ManualCalculationApiFactory();
        using var configured = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        { s.RemoveAll<IComparisonSnapshotStore>(); s.AddSingleton<IComparisonSnapshotStore>(store); }));
        using var client = configured.CreateClient(); using var cancel = new CancellationTokenSource();
        var first = client.PostAsJsonAsync(Route, Request(), cancel.Token);
        var second = client.PostAsJsonAsync(Route, Request());
        try
        {
            await store.TwoStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var busy = await Json(await client.PostAsJsonAsync(Route, Request()), HttpStatusCode.ServiceUnavailable);
            Assert.Equal("comparisonBusy", busy["code"]!.GetValue<string>());
            Assert.Equal(2, store.Calls);
            cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await first);
            await store.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally { cancel.Cancel(); store.Release.TrySetResult(); }
        await Json(await second);
        await Json(await client.PostAsJsonAsync(Route, Request()));
        Assert.Equal(3, store.Calls);
    }

    [Fact]
    public async Task Server_deadline_is_a_typed_failure_and_does_not_cancel_the_next_request()
    {
        var clock = new DeadlineClock(); var store = new ControlledSnapshot(); using var factory = new ManualCalculationApiFactory();
        using var configured = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IComparisonSnapshotStore>(); s.AddSingleton<IComparisonSnapshotStore>(store);
            s.RemoveAll<TimeProvider>(); s.AddSingleton<TimeProvider>(clock);
        }));
        using var client = configured.CreateClient();
        var pending = client.PostAsJsonAsync(Route, Request());
        try
        {
            await store.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(TimeSpan.FromSeconds(120), clock.DueTime);
            clock.Expire();
            var error = await Json(await pending, HttpStatusCode.ServiceUnavailable);
            Assert.Equal("comparisonTimedOut", error["code"]!.GetValue<string>());
        }
        finally { store.Release.TrySetResult(); }
        await Json(await client.PostAsJsonAsync(Route, Request()));
        Assert.Equal(2, store.Calls);
    }

    [Fact]
    public async Task A_failed_group_cannot_return_partial_results_or_expose_storage_details()
    {
        var store = new ControlledSnapshot { Fail = true }; using var factory = new ManualCalculationApiFactory();
        using var configured = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        { s.RemoveAll<IComparisonSnapshotStore>(); s.AddSingleton<IComparisonSnapshotStore>(store); }));
        using var client = configured.CreateClient(); store.Release.TrySetResult();
        var error = await Json(await client.PostAsJsonAsync(Route, Request()), HttpStatusCode.ServiceUnavailable);
        Assert.Equal("comparisonStorageUnavailable", error["code"]!.GetValue<string>());
        Assert.Null(error["views"]); Assert.DoesNotContain("private-detail", error.ToJsonString());
        store.Fail = false;
        await Json(await client.PostAsJsonAsync(Route, Request()));
    }

    private sealed class ControlledSnapshot : IComparisonSnapshotStore
    {
        private int _calls;
        public int Calls => _calls;
        public bool Fail { get; set; }
        public bool MissingGroup { get; set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource TwoStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<ComparisonBaseline> ReadBaselineAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<ComparisonSnapshot> ReadAsync(IReadOnlyList<Guid> vehicleIds, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public async Task<CompleteComparisonSnapshot> ReadAllAsync(string expectedBaselineToken, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _calls) == 2) TwoStarted.TrySetResult();
            Started.TrySetResult();
            try { await Release.Task.WaitAsync(cancellationToken); }
            catch (OperationCanceledException) { Cancelled.TrySetResult(); throw; }
            if (Fail) throw new InvalidDataException("private-detail: failed comparison group");
            var baseline = new ComparisonBaseline(new(null, 0), new(null, 0), MissingGroup ? 1 : 0, Token);
            return new(baseline, new(baseline.Profile, baseline.Rules, []));
        }
    }

    private sealed class DeadlineClock : TimeProvider
    {
        private Action? _expire;
        public TimeSpan DueTime { get; private set; }
        public void Expire() => _expire!();
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            DueTime = dueTime; _expire = () => callback(state); return new TimerHandle();
        }
        private sealed class TimerHandle : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
