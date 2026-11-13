using System.Globalization;
using System.Net;
using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using PitWall.Jev;
using PitWall.Jev.Caching;
using PitWall.Jev.Guards;
using PitWall.Jev.Real;

namespace PitWall.Tests.Jev.Real;

public sealed class JevClientTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 8, 17, 0, 0, TimeSpan.Zero));
    private readonly StaticOptionsMonitor<JevOptions> _options = new(new JevOptions
    {
        Client = JevClientKind.Real,
        ApiKey = TypeSafeExamples.TestApiKey,
    });

    [Fact]
    public async Task SystemOneAsync_StrategyRequest_PostsOfficialContract()
    {
        var handler = StubTypeSafeHandler.Json(TypeSafeExamples.StrategyLap60Response);

        await CreateClient(handler).SystemOneAsync(JevTestRequests.StrategyLap60(), CancellationToken.None);

        var sent = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal(new Uri("https://api.typesafe.ai/v1/systemone"), sent.Uri);
        Assert.Equal($"Bearer {TypeSafeExamples.TestApiKey}", sent.Authorization);
        Assert.Equal("application/json", sent.ContentType);

        using var body = JsonDocument.Parse(sent.Body);
        var root = body.RootElement;
        Assert.Equal(["model", "state", "questions"], root.EnumerateObject().Select(p => p.Name));
        Assert.Equal("jev-latest", root.GetProperty("model").GetString());
        Assert.Equal(JevTestRequests.StrategyStateLap60, root.GetProperty("state").GetString());

        var pitCall = root.GetProperty("questions").GetProperty("pit_call");
        Assert.Equal("choice", pitCall.GetProperty("type").GetString());
        Assert.Equal("Decide the pit call for the focus car this lap.", pitCall.GetProperty("instructions").GetString());
        var criteria = pitCall.GetProperty("criteria");
        Assert.Equal(JsonValueKind.Object, criteria.ValueKind);
        Assert.Equal(["STAY_OUT", "BOX", "BOX_NEXT_LAP"], criteria.EnumerateObject().Select(p => p.Name));
        Assert.All(criteria.EnumerateObject(), option => Assert.Equal(JsonValueKind.Null, option.Value.ValueKind));
        Assert.Equal(4, root.GetProperty("questions").EnumerateObject().Count());
    }

    [Fact]
    public async Task SystemOneAsync_ScoreWithoutCriteria_SendsLevelsOneToFive()
    {
        var handler = StubTypeSafeHandler.Json(TypeSafeExamples.TriageResponse);

        await CreateClient(handler).SystemOneAsync(JevTestRequests.Triage("RED FLAG"), CancellationToken.None);

        using var body = JsonDocument.Parse(Assert.Single(handler.Requests).Body);
        var urgency = body.RootElement.GetProperty("questions").GetProperty("urgency");
        Assert.Equal("score", urgency.GetProperty("type").GetString());
        Assert.Equal(["1", "2", "3", "4", "5"], urgency.GetProperty("criteria").EnumerateArray().Select(level => level.GetString()));
    }

    [Fact]
    public async Task SystemOneAsync_NoulQuestion_OmitsCriteria()
    {
        var handler = StubTypeSafeHandler.Json(TypeSafeExamples.BillingNoulResponse);

        await CreateClient(handler).SystemOneAsync(BillingRequest(), CancellationToken.None);

        using var body = JsonDocument.Parse(Assert.Single(handler.Requests).Body);
        var billing = body.RootElement.GetProperty("questions").GetProperty("billing");
        Assert.Equal(["type", "instructions"], billing.EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public async Task SystemOneAsync_OfficialChoiceExample_MapsAnswerAndInputTokens()
    {
        var request = new JevRequest("jev-latest", "I was charged twice.", new Dictionary<string, JevQuestion>
        {
            ["tone"] = new(JevQuestionType.Choice, "What is the tone of this message?", ["angry", "calm", "excited"]),
        });

        var response = await CreateClient(StubTypeSafeHandler.Json(TypeSafeExamples.ToneChoiceResponse))
            .SystemOneAsync(request, CancellationToken.None);

        var tone = response.Answers["tone"];
        Assert.Equal("calm", tone.Choice);
        Assert.Equal(0.7, tone.Confidence);
        Assert.Equal(0.8, tone.Probabilities!["calm"]);
        Assert.Equal(120, response.Tokens);
    }

    [Fact]
    public async Task SystemOneAsync_OfficialScoreExample_KeepsApiScaleForTextLevels()
    {
        var request = new JevRequest("jev-latest", "I was charged twice.", new Dictionary<string, JevQuestion>
        {
            ["urgency"] = new(JevQuestionType.Score, "How urgent is this message?", ["Can wait", "Needs attention this week", "Needs attention today"]),
        });

        var response = await CreateClient(StubTypeSafeHandler.Json(TypeSafeExamples.UrgencyScoreResponse))
            .SystemOneAsync(request, CancellationToken.None);

        var urgency = response.Answers["urgency"];
        Assert.Equal(1.7, urgency.Score);
        Assert.Equal(0.55, urgency.Confidence);
        Assert.Equal(0.8, urgency.Probabilities!["Needs attention today"]);
    }

    [Fact]
    public async Task SystemOneAsync_TriageUrgency_ReturnsScoreOnOneToFiveScale()
    {
        var response = await CreateClient(StubTypeSafeHandler.Json(TypeSafeExamples.TriageResponse))
            .SystemOneAsync(JevTestRequests.Triage("RED FLAG"), CancellationToken.None);

        Assert.Equal("ACT_NOW", response.Answers["impact"].Choice);
        Assert.Equal(4.2, response.Answers["urgency"].Score!.Value, 6);
        Assert.Equal(0.45, response.Answers["urgency"].Probabilities!["4"]);
        Assert.Equal(210, response.Tokens);
    }

    [Fact]
    public async Task SystemOneAsync_OfficialNoulExample_DerivesConfidenceFromProbability()
    {
        var response = await CreateClient(StubTypeSafeHandler.Json(TypeSafeExamples.BillingNoulResponse))
            .SystemOneAsync(BillingRequest(), CancellationToken.None);

        var billing = response.Answers["billing"];
        Assert.Equal(0.98, billing.Score);
        Assert.Equal(0.96, billing.Confidence, 6);
        Assert.Null(billing.Choice);
    }

    [Fact]
    public async Task SystemOneAsync_WithoutApiKey_ThrowsWithoutCalling()
    {
        _options.CurrentValue.ApiKey = " ";
        var handler = StubTypeSafeHandler.Json(TypeSafeExamples.StrategyLap60Response);

        await Assert.ThrowsAsync<JevClientException>(
            () => CreateClient(handler).SystemOneAsync(JevTestRequests.StrategyLap60(), CancellationToken.None));

        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData((HttpStatusCode)529)]
    public async Task SystemOneAsync_ErrorStatus_ThrowsJevClientException(HttpStatusCode status)
    {
        var handler = StubTypeSafeHandler.Json("""{"detail":"rejected"}""", status);

        var error = await Assert.ThrowsAsync<JevClientException>(
            () => CreateClient(handler).SystemOneAsync(JevTestRequests.StrategyLap60(), CancellationToken.None));

        Assert.Contains(((int)status).ToString(CultureInfo.InvariantCulture), error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(TypeSafeExamples.TestApiKey, error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"model":"jev-latest","answers":{}}""")]
    [InlineData("""{"model":"jev-latest","answers":{"pit_call":{"type":"choice","choice":"BOX","confidence":0.8,"probabilities":{"BOX":0.8}}},"usage":{"input_tokens":10,"output_tokens":1}}""")]
    [InlineData("""{"model":"jev-latest","answers":{"tone":{"type":"choice","choice":"calm","confidence":0.7,"probabilities":{"calm":0.8}}},"usage":{"input_tokens":10,"output_tokens":1}}""")]
    public async Task SystemOneAsync_ResponseOutsideContract_ThrowsJevClientException(string body)
    {
        await Assert.ThrowsAsync<JevClientException>(
            () => CreateClient(StubTypeSafeHandler.Json(body)).SystemOneAsync(JevTestRequests.StrategyLap60(), CancellationToken.None));
    }

    [Fact]
    public async Task SystemOneAsync_ChoiceNotSent_ThrowsJevClientException()
    {
        var body = TypeSafeExamples.StrategyLap60Response.Replace("\"choice\":\"BOX\"", "\"choice\":\"PIT\"", StringComparison.Ordinal);

        await Assert.ThrowsAsync<JevClientException>(
            () => CreateClient(StubTypeSafeHandler.Json(body)).SystemOneAsync(JevTestRequests.StrategyLap60(), CancellationToken.None));
    }

    [Fact]
    public async Task SystemOneAsync_AnswerTypeMismatch_ThrowsJevClientException()
    {
        var body = TypeSafeExamples.TriageResponse.Replace("\"type\":\"score\"", "\"type\":\"noul\"", StringComparison.Ordinal);

        await Assert.ThrowsAsync<JevClientException>(
            () => CreateClient(StubTypeSafeHandler.Json(body)).SystemOneAsync(JevTestRequests.Triage("RED FLAG"), CancellationToken.None));
    }

    [Fact]
    public async Task SystemOneAsync_BoxQuestion_ThrowsWithoutCalling()
    {
        var handler = StubTypeSafeHandler.Json(TypeSafeExamples.BillingNoulResponse);
        var request = new JevRequest("jev-latest", "photo", new Dictionary<string, JevQuestion>
        {
            ["car"] = new(JevQuestionType.Box, "Locate the car."),
        });

        await Assert.ThrowsAsync<JevClientException>(() => CreateClient(handler).SystemOneAsync(request, CancellationToken.None));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SystemOneAsync_NoResponseWithinTimeout_ThrowsJevClientException()
    {
        _options.CurrentValue.TimeoutMs = 2000;
        var pending = CreateClient(StubTypeSafeHandler.Hanging()).SystemOneAsync(JevTestRequests.StrategyLap60(), CancellationToken.None);

        _time.Advance(TimeSpan.FromMilliseconds(2000));

        await Assert.ThrowsAsync<JevClientException>(() => pending);
    }

    [Fact]
    public async Task SystemOneAsync_CallerCancels_ThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        var pending = CreateClient(StubTypeSafeHandler.Hanging()).SystemOneAsync(JevTestRequests.StrategyLap60(), cts.Token);

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public async Task SystemOneAsync_GuardedRealTwice_BillsInputTokensOnceThenCache()
    {
        var handler = StubTypeSafeHandler.Json(TypeSafeExamples.StrategyLap60Response);
        using var meterFactory = new TestMeterFactory();
        var guarded = new GuardedJevClient(
            CreateClient(handler),
            JevClientKind.Real,
            new InMemoryJevCacheStore(),
            _options,
            new JevMetrics(meterFactory),
            new JevAlerts(),
            _time,
            NullLogger<GuardedJevClient>.Instance);

        var first = await guarded.SystemOneAsync(JevTestRequests.StrategyLap60(), CancellationToken.None);
        var second = await guarded.SystemOneAsync(JevTestRequests.StrategyLap60(), CancellationToken.None);

        Assert.Single(handler.Requests);
        Assert.Equal(640, first.Tokens);
        Assert.Equal(640 * 0.042m / 1_000_000m, first.CostUsd);
        Assert.True(second.FromCache);
        Assert.Equal(0m, second.CostUsd);
        Assert.Equal("BOX", second.Answers["pit_call"].Choice);
    }

    private JevClient CreateClient(HttpMessageHandler handler) =>
        new(new StubHttpClientFactory(handler), _options, _time, NullLogger<JevClient>.Instance);

    private static JevRequest BillingRequest() => new("jev-latest", "I was charged twice.", new Dictionary<string, JevQuestion>
    {
        ["billing"] = new(JevQuestionType.Noul, "Is this message about billing?"),
    });
}
