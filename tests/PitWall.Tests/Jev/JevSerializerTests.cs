using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

using PitWall.Domain;
using PitWall.Jev;
using PitWall.Jev.Serialization;

namespace PitWall.Tests.Jev;

public sealed class JevSerializerTests
{
    [Fact]
    public void SerializeRequest_StrategyLap60_MatchesFixture()
    {
        var expected = Compact(JevFixtures.Read("strategy_request_lap60.json"));

        var json = JevSerializer.SerializeRequest(JevTestRequests.StrategyLap60());

        Assert.Equal(expected, json);
    }

    [Fact]
    public void SerializeRequest_Metadata_NotSentToApi()
    {
        var json = JevSerializer.SerializeRequest(JevTestRequests.StrategyLap60() with { Kind = JevKind.Triage, SessionKey = 1 });

        Assert.DoesNotContain("kind", json, StringComparison.Ordinal);
        Assert.DoesNotContain("session", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ComputeStateHash_SameNormalizedRequest_SameHash()
    {
        var request = JevTestRequests.StrategyLap60();
        var reordered = request with
        {
            Questions = request.Questions.Reverse().ToDictionary(),
            SessionKey = request.SessionKey + 1,
        };

        var hash = JevSerializer.ComputeStateHash(request);

        Assert.Matches("^[0-9a-f]{64}$", hash);
        Assert.Equal(hash, JevSerializer.ComputeStateHash(reordered));
    }

    [Fact]
    public void ComputeStateHash_DifferentState_DifferentHash()
    {
        Assert.NotEqual(
            JevSerializer.ComputeStateHash(JevTestRequests.StrategyForLap(60)),
            JevSerializer.ComputeStateHash(JevTestRequests.StrategyForLap(61)));
    }

    [Fact]
    public void DeserializeAnswers_StrategyResponseFixture_ReadsChoicesAndProbabilities()
    {
        var answers = JevSerializer.DeserializeAnswers(JevFixtures.Read("strategy_response_lap60.json"));

        var pitCall = answers["pit_call"];
        Assert.Equal("BOX", pitCall.Choice);
        Assert.Equal(0.84, pitCall.Confidence);
        Assert.Equal(0.07, pitCall.Probabilities!["BOX_NEXT_LAP"]);
        Assert.Equal("INTERMEDIATE", answers["compound"].Choice);
        Assert.Equal("MANAGE", answers["pace_mode"].Choice);
        Assert.Equal("BALANCED", answers["energy_mode"].Choice);
    }

    [Fact]
    public void SerializeAnswers_RoundTrip_PreservesAnswers()
    {
        var answers = JevSerializer.DeserializeAnswers(JevFixtures.Read("strategy_response_lap60.json"));

        var roundTrip = JevSerializer.DeserializeAnswers(JevSerializer.SerializeAnswers(answers));

        Assert.Equal(JevSerializer.SerializeAnswers(answers), JevSerializer.SerializeAnswers(roundTrip));
        Assert.Null(roundTrip["pit_call"].Score);
    }

    private static string Compact(string json) =>
        JsonNode.Parse(json)!.ToJsonString(new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
}
