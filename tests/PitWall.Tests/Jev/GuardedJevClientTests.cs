using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using PitWall.Jev;
using PitWall.Jev.Caching;
using PitWall.Jev.Guards;
using PitWall.Jev.Serialization;

namespace PitWall.Tests.Jev;

public sealed class GuardedJevClientTests : IDisposable
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 8, 17, 0, 0, TimeSpan.Zero));
    private readonly TestMeterFactory _meterFactory = new();
    private readonly MetricCapture _metrics;
    private readonly JevAlerts _alerts = new();
    private readonly StaticOptionsMonitor<JevOptions> _options = new(new JevOptions());
    private readonly CountingJevClient _inner;

    public GuardedJevClientTests()
    {
        _metrics = new MetricCapture(_meterFactory);
        _inner = new CountingJevClient(_time);
    }

    [Fact]
    public async Task SystemOneAsync_Disabled_DoesNotCallClient()
    {
        _options.CurrentValue.Enabled = false;
        var client = CreateClient();

        var response = await client.SystemOneAsync(JevTestRequests.StrategyLap60(), CancellationToken.None);

        Assert.Equal(JevResponseStatus.Disabled, response.Status);
        Assert.Empty(response.Answers);
        Assert.Equal(0, _inner.Calls);
        Assert.Empty(_metrics.For("pitwall.jev.requests"));
    }

    [Fact]
    public async Task SystemOneAsync_SameRequestTwice_SecondFromCacheWithoutCall()
    {
        var client = CreateClient();

        var first = await client.SystemOneAsync(JevTestRequests.StrategyLap60(), CancellationToken.None);
        var second = await client.SystemOneAsync(JevTestRequests.StrategyLap60(), CancellationToken.None);

        Assert.Equal(1, _inner.Calls);
        Assert.False(first.FromCache);
        Assert.True(second.FromCache);
        Assert.Equal(JevResponseStatus.Answered, second.Status);
        Assert.Equal(JevSerializer.SerializeAnswers(first.Answers), JevSerializer.SerializeAnswers(second.Answers));
        Assert.Equal(0, second.Tokens);
        Assert.Equal(0m, second.CostUsd);
        Assert.Equal(1, client.RequestCount(JevTestRequests.SessionKey));
    }

    [Fact]
    public async Task SystemOneAsync_SameStateDifferentQuestionOrder_HitsCache()
    {
        var client = CreateClient();
        var reordered = JevTestRequests.StrategyLap60() with
        {
            Questions = JevTestRequests.StrategyLap60().Questions.Reverse().ToDictionary(),
        };

        await client.SystemOneAsync(JevTestRequests.StrategyLap60(), CancellationToken.None);
        var second = await client.SystemOneAsync(reordered, CancellationToken.None);

        Assert.True(second.FromCache);
        Assert.Equal(1, _inner.Calls);
    }

    [Fact]
    public async Task SystemOneAsync_CacheDisabled_CallsClientEveryTime()
    {
        _options.CurrentValue.Cache.Enabled = false;
        var client = CreateClient();

        await client.SystemOneAsync(JevTestRequests.StrategyLap60(), CancellationToken.None);
        var second = await client.SystemOneAsync(JevTestRequests.StrategyLap60(), CancellationToken.None);

        Assert.Equal(2, _inner.Calls);
        Assert.False(second.FromCache);
    }

    [Fact]
    public async Task SystemOneAsync_MaxRequestsReached_StopsCallingAndRaisesAlertOnce()
    {
        _options.CurrentValue.MaxRequestsPerSession = 2;
        var client = CreateClient();
        var raised = new List<JevAlert>();
        _alerts.Raised += raised.Add;

        var responses = new List<JevResponse>();
        for (var lap = 1; lap <= 4; lap++)
        {
            responses.Add(await client.SystemOneAsync(JevTestRequests.StrategyForLap(lap), CancellationToken.None));
        }

        Assert.Equal(2, _inner.Calls);
        Assert.Equal(
            new[] { JevResponseStatus.Answered, JevResponseStatus.Answered, JevResponseStatus.RequestLimitReached, JevResponseStatus.RequestLimitReached },
            responses.Select(r => r.Status));
        var alert = Assert.Single(raised);
        Assert.Equal(JevAlertKind.RequestLimitReached, alert.Kind);
        Assert.Equal(JevTestRequests.SessionKey, alert.SessionKey);
        Assert.Equal(_time.GetUtcNow(), alert.RaisedAt);
        Assert.Single(_alerts.Active);
    }

    [Fact]
    public async Task SystemOneAsync_MaxRequestsReached_CachedRequestStillAnswered()
    {
        _options.CurrentValue.MaxRequestsPerSession = 1;
        var client = CreateClient();

        await client.SystemOneAsync(JevTestRequests.StrategyLap60(), CancellationToken.None);
        var cached = await client.SystemOneAsync(JevTestRequests.StrategyLap60(), CancellationToken.None);
        var blocked = await client.SystemOneAsync(JevTestRequests.StrategyForLap(61), CancellationToken.None);

        Assert.True(cached.FromCache);
        Assert.Equal(JevResponseStatus.RequestLimitReached, blocked.Status);
        Assert.Equal(1, _inner.Calls);
    }

    [Fact]
    public async Task SystemOneAsync_MaxRequestsReachedInOneSession_OtherSessionStillCalls()
    {
        _options.CurrentValue.MaxRequestsPerSession = 1;
        var client = CreateClient();

        await client.SystemOneAsync(JevTestRequests.StrategyForLap(1), CancellationToken.None);
        var otherSession = await client.SystemOneAsync(
            JevTestRequests.StrategyForLap(2) with { SessionKey = JevTestRequests.SessionKey + 1 },
            CancellationToken.None);

        Assert.Equal(JevResponseStatus.Answered, otherSession.Status);
        Assert.Equal(2, _inner.Calls);
    }

    [Fact]
    public async Task SystemOneAsync_ManyCacheHits_DoNotConsumeBudget()
    {
        _options.CurrentValue.MaxRequestsPerSession = 2;
        var client = CreateClient();

        await client.SystemOneAsync(JevTestRequests.StrategyLap60(), CancellationToken.None);
        for (var i = 0; i < 10; i++)
        {
            Assert.True((await client.SystemOneAsync(JevTestRequests.StrategyLap60(), CancellationToken.None)).FromCache);
        }

        var next = await client.SystemOneAsync(JevTestRequests.StrategyForLap(61), CancellationToken.None);

        Assert.Equal(JevResponseStatus.Answered, next.Status);
        Assert.Equal(2, _inner.Calls);
        Assert.Equal(2, client.RequestCount(JevTestRequests.SessionKey));
    }

    [Fact]
    public async Task SystemOneAsync_NewReplaySession_StartsWithCounterAtZero()
    {
        _options.CurrentValue.MaxRequestsPerSession = 1;
        var client = CreateClient();
        var otherSession = JevTestRequests.SessionKey + 1;

        await client.SystemOneAsync(JevTestRequests.StrategyForLap(1), CancellationToken.None);
        await client.SystemOneAsync(JevTestRequests.StrategyForLap(2) with { SessionKey = otherSession }, CancellationToken.None);

        Assert.Equal(0, client.RequestCount(JevTestRequests.SessionKey));
        Assert.Equal(1, client.RequestCount(otherSession));
    }

    [Fact]
    public async Task SystemOneAsync_SessionReloadedAfterLimit_CallsAgainAndClearsAlert()
    {
        _options.CurrentValue.MaxRequestsPerSession = 1;
        var client = CreateClient();
        var raised = new List<JevAlert>();
        _alerts.Raised += raised.Add;

        await client.SystemOneAsync(JevTestRequests.StrategyForLap(1), CancellationToken.None);
        var limited = await client.SystemOneAsync(JevTestRequests.StrategyForLap(2), CancellationToken.None);
        await client.SystemOneAsync(JevTestRequests.StrategyForLap(3) with { SessionKey = JevTestRequests.SessionKey + 1 }, CancellationToken.None);
        var reloaded = await client.SystemOneAsync(JevTestRequests.StrategyForLap(2), CancellationToken.None);

        Assert.Equal(JevResponseStatus.RequestLimitReached, limited.Status);
        Assert.Equal(JevResponseStatus.Answered, reloaded.Status);
        Assert.Equal(1, client.RequestCount(JevTestRequests.SessionKey));
        Assert.Equal(3, _inner.Calls);
        Assert.Single(raised);
        Assert.Empty(_alerts.Active);
    }

    [Fact]
    public async Task SystemOneAsync_LimitReachedAgainAfterReload_RaisesAlertAgain()
    {
        _options.CurrentValue.MaxRequestsPerSession = 1;
        var client = CreateClient();
        var raised = new List<JevAlert>();
        _alerts.Raised += raised.Add;

        for (var pass = 0; pass < 2; pass++)
        {
            await client.SystemOneAsync(JevTestRequests.StrategyForLap(10 + pass), CancellationToken.None);
            await client.SystemOneAsync(JevTestRequests.StrategyForLap(20 + pass), CancellationToken.None);
            await client.SystemOneAsync(JevTestRequests.StrategyForLap(30 + pass) with { SessionKey = JevTestRequests.SessionKey + 1 }, CancellationToken.None);
        }

        Assert.Equal(2, raised.Count(a => a.SessionKey == JevTestRequests.SessionKey));
    }

    [Fact]
    public async Task SystemOneAsync_FakeClient_CostIsZeroAndLatencyMeasured()
    {
        var client = CreateClient(JevClientKind.Fake);

        var response = await client.SystemOneAsync(JevTestRequests.StrategyLap60(), CancellationToken.None);

        Assert.Equal(0m, response.CostUsd);
        Assert.Equal(212, response.LatencyMs);
        Assert.True(response.Tokens > 0);
    }

    [Fact]
    public async Task SystemOneAsync_RealClient_CostFromTokensAndPrice()
    {
        var inner = new CountingJevClient(_time) { Tokens = 640 };
        var client = CreateClient(JevClientKind.Real, inner);

        var response = await client.SystemOneAsync(JevTestRequests.StrategyLap60(), CancellationToken.None);

        // 640 tokens × US$0,042 / millón.
        Assert.Equal(0.00002688m, response.CostUsd);
        Assert.Equal(640, response.Tokens);
    }

    [Fact]
    public async Task SystemOneAsync_ClientFails_ReturnsFailedAndRaisesAlert()
    {
        _inner.Fail = true;
        var client = CreateClient();

        var response = await client.SystemOneAsync(JevTestRequests.StrategyLap60(), CancellationToken.None);

        Assert.Equal(JevResponseStatus.Failed, response.Status);
        Assert.Equal(JevAlertKind.ClientFailed, Assert.Single(_alerts.Active).Kind);
    }

    [Fact]
    public async Task SystemOneAsync_UnavailableRealClient_ReturnsFailedWithoutThrowing()
    {
        var client = CreateClient(JevClientKind.Real, new UnavailableJevClient());

        var response = await client.SystemOneAsync(JevTestRequests.StrategyLap60(), CancellationToken.None);

        Assert.Equal(JevResponseStatus.Failed, response.Status);
        Assert.Equal(JevAlertKind.ClientFailed, Assert.Single(_alerts.Active).Kind);
    }

    [Fact]
    public async Task SystemOneAsync_CallAndCacheHit_RecordsMetricsWithTags()
    {
        var inner = new CountingJevClient(_time) { Tokens = 640 };
        var client = CreateClient(JevClientKind.Real, inner);

        await client.SystemOneAsync(JevTestRequests.StrategyLap60(), CancellationToken.None);
        await client.SystemOneAsync(JevTestRequests.StrategyLap60(), CancellationToken.None);

        var requests = _metrics.For("pitwall.jev.requests");
        Assert.Equal(2, requests.Count);
        Assert.All(requests, m =>
        {
            Assert.Equal(1, m.Value);
            Assert.Equal("strategy", m.Tags["kind"]);
            Assert.Equal("real", m.Tags["client"]);
        });
        Assert.Equal(new[] { "miss", "hit" }, requests.Select(m => m.Tags["cache"]));

        Assert.Equal(640, Assert.Single(_metrics.For("pitwall.jev.tokens")).Value);
        Assert.Equal(212, Assert.Single(_metrics.For("pitwall.jev.latency")).Value);
        Assert.Equal(0.00002688, Assert.Single(_metrics.For("pitwall.jev.cost_usd")).Value, 1e-12);
    }

    [Fact]
    public async Task SystemOneAsync_TriageWithFakeClient_TagsTriageAndFake()
    {
        var client = CreateClient(JevClientKind.Fake);

        await client.SystemOneAsync(JevTestRequests.Triage("SAFETY CAR DEPLOYED"), CancellationToken.None);

        var request = Assert.Single(_metrics.For("pitwall.jev.requests"));
        Assert.Equal("triage", request.Tags["kind"]);
        Assert.Equal("fake", request.Tags["client"]);
        Assert.Equal("miss", request.Tags["cache"]);
        Assert.Equal(0, Assert.Single(_metrics.For("pitwall.jev.cost_usd")).Value);
    }

    public void Dispose()
    {
        _metrics.Dispose();
        _meterFactory.Dispose();
    }

    private GuardedJevClient CreateClient(JevClientKind clientKind = JevClientKind.Fake, IJevClient? inner = null) => new(
        inner ?? _inner,
        clientKind,
        new InMemoryJevCacheStore(),
        _options,
        new JevMetrics(_meterFactory),
        _alerts,
        _time,
        NullLogger<GuardedJevClient>.Instance);
}
