using System.Net;
using System.Net.Http.Headers;

using Microsoft.Extensions.Logging.Abstractions;

using PitWall.Ingestion;
using PitWall.Ingestion.OpenF1;

namespace PitWall.Tests.Ingestion;

public sealed class OpenF1RateLimitHandlerTests
{
    private readonly RecordingTimeProvider _time = new();

    [Fact]
    public async Task SendAsync_ConsecutiveRequests_PausesDefault500MsBetweenRequests()
    {
        using var http = CreateClient(new ScriptedHandler(), new OpenF1Options());

        await http.GetAsync(new Uri("laps?session_key=11412", UriKind.Relative));
        await http.GetAsync(new Uri("pit?session_key=11412", UriKind.Relative));
        await http.GetAsync(new Uri("weather?session_key=11412", UriKind.Relative));

        Assert.Equal([TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500)], _time.Delays);
    }

    [Fact]
    public async Task SendAsync_On429Then503_RetriesWithExponentialBackoff()
    {
        var inner = new ScriptedHandler(
            ScriptedHandler.Status(HttpStatusCode.TooManyRequests),
            ScriptedHandler.Status(HttpStatusCode.ServiceUnavailable));
        using var http = CreateClient(inner, new OpenF1Options { RequestDelay = TimeSpan.Zero });

        using var response = await http.GetAsync(new Uri("laps?session_key=11412", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, inner.Calls);
        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)], _time.Delays);
    }

    [Fact]
    public async Task SendAsync_429WithRetryAfter_WaitsServerDelay()
    {
        var throttled = ScriptedHandler.Status(HttpStatusCode.TooManyRequests);
        throttled.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(7));
        var inner = new ScriptedHandler(throttled);
        using var http = CreateClient(inner, new OpenF1Options { RequestDelay = TimeSpan.Zero });

        using var response = await http.GetAsync(new Uri("laps?session_key=11412", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([TimeSpan.FromSeconds(7)], _time.Delays);
    }

    [Fact]
    public async Task SendAsync_NotFound_DoesNotRetry()
    {
        var inner = new ScriptedHandler(ScriptedHandler.Status(HttpStatusCode.NotFound));
        using var http = CreateClient(inner, new OpenF1Options { RequestDelay = TimeSpan.Zero });

        using var response = await http.GetAsync(new Uri("starting_grid?session_key=11412", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(1, inner.Calls);
        Assert.Empty(_time.Delays);
    }

    [Fact]
    public async Task GetAsync_5xxAfterMaxRetries_ThrowsOpenF1IngestionException()
    {
        var options = new OpenF1Options { RequestDelay = TimeSpan.Zero, MaxRetries = 2 };
        var inner = new ScriptedHandler(
            ScriptedHandler.Status(HttpStatusCode.InternalServerError),
            ScriptedHandler.Status(HttpStatusCode.BadGateway),
            ScriptedHandler.Status(HttpStatusCode.ServiceUnavailable));
        using var http = CreateClient(inner, options);
        var client = new OpenF1Client(http, new OpenF1FixtureRecorder(null));

        await Assert.ThrowsAsync<OpenF1IngestionException>(() =>
            client.GetAsync("laps", 11412, OpenF1JsonContext.Default.ListOpenF1Lap, CancellationToken.None));

        Assert.Equal(3, inner.Calls);
        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)], _time.Delays);
    }

    private HttpClient CreateClient(HttpMessageHandler inner, OpenF1Options options) =>
        new(new OpenF1RateLimitHandler(options, _time, NullLogger<OpenF1RateLimitHandler>.Instance) { InnerHandler = inner })
        {
            BaseAddress = options.BaseAddress,
        };
}
