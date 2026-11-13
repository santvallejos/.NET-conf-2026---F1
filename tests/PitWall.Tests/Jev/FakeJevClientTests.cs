using PitWall.Domain;
using PitWall.Jev;
using PitWall.Jev.Fake;
using PitWall.Jev.Serialization;
using PitWall.Jev.State;

namespace PitWall.Tests.Jev;

public sealed class FakeJevClientTests
{
    private readonly FakeJevClient _client = new();

    [Fact]
    public async Task SystemOneAsync_StrategyLap60_ChoosesBestExpectedPosition()
    {
        var response = await _client.SystemOneAsync(JevTestRequests.StrategyLap60(), CancellationToken.None);

        // Mejor exp_pos: BOX_L_INTERMEDIATE (11.9).
        Assert.Equal("BOX", response.Answers["pit_call"].Choice);
        Assert.Equal("INTERMEDIATE", response.Answers["compound"].Choice);
    }

    [Fact]
    public async Task SystemOneAsync_StrategyLap60_ProbabilitiesDerivedFromMonteCarlo()
    {
        var response = await _client.SystemOneAsync(JevTestRequests.StrategyLap60(), CancellationToken.None);

        var pitCall = response.Answers["pit_call"];
        Assert.NotNull(pitCall.Probabilities);
        Assert.Equal(1.0, pitCall.Probabilities.Values.Sum(), 0.001);
        Assert.Equal(pitCall.Probabilities.Values.Max(), pitCall.Probabilities["BOX"]);
        Assert.True(pitCall.Probabilities["BOX_NEXT_LAP"] > pitCall.Probabilities["STAY_OUT"]);
        Assert.Equal(pitCall.Probabilities["BOX"], pitCall.Confidence);
    }

    [Fact]
    public async Task SystemOneAsync_QuestionNotInformedByMonteCarlo_ChoosesFirstCriterion()
    {
        var response = await _client.SystemOneAsync(JevTestRequests.StrategyLap60(), CancellationToken.None);

        Assert.Equal("PUSH", response.Answers["pace_mode"].Choice);
        Assert.Equal("HARVEST", response.Answers["energy_mode"].Choice);
        Assert.Equal(0.7, response.Answers["pace_mode"].Confidence);
    }

    [Fact]
    public async Task SystemOneAsync_SameRequest_SameAnswers()
    {
        var first = await _client.SystemOneAsync(JevTestRequests.StrategyLap60(), CancellationToken.None);
        var second = await new FakeJevClient().SystemOneAsync(JevTestRequests.StrategyLap60(), CancellationToken.None);

        Assert.Equal(JevSerializer.SerializeAnswers(first.Answers), JevSerializer.SerializeAnswers(second.Answers));
        Assert.Equal(first.Tokens, second.Tokens);
    }

    [Fact]
    public async Task SystemOneAsync_AnyRequest_CostZeroAndTokensEstimated()
    {
        var request = JevTestRequests.StrategyLap60();

        var response = await _client.SystemOneAsync(request, CancellationToken.None);

        Assert.Equal(0m, response.CostUsd);
        Assert.Equal(JevTokenEstimator.Estimate(JevSerializer.SerializeRequest(request)), response.Tokens);
        Assert.False(response.FromCache);
    }

    [Fact]
    public async Task SystemOneAsync_StateFromStrategyOutcomes_MapsCallAndCompound()
    {
        StrategyOutcome[] outcomes =
        [
            Outcome("STAY_OUT", PitCall.StayOut, null, null, 9.4),
            Outcome("BOX_L_MEDIUM", PitCall.Box, 30, Compound.Medium, 8.7),
            Outcome("BOX_L+1_HARD", PitCall.Box, 31, Compound.Hard, 7.94),
            Outcome("BOX_L+2_HARD", PitCall.Box, 32, Compound.Hard, 8.1),
        ];
        var mc = JevSerializer.SerializeMonteCarlo(outcomes.Select(JevMonteCarloEntry.From));
        var state = $"{{\"mc\":{mc}}}";

        var response = await _client.SystemOneAsync(JevTestRequests.Strategy(state), CancellationToken.None);

        Assert.Equal("BOX_NEXT_LAP", response.Answers["pit_call"].Choice);
        Assert.Equal("HARD", response.Answers["compound"].Choice);
    }

    [Fact]
    public async Task SystemOneAsync_BestCandidateBoxesInTwoLaps_StaysOut()
    {
        const string state = """{"mc":[{"id":"BOX_L+2_HARD","exp_pos":6.0,"p_gain":0.5},{"id":"BOX_L_MEDIUM","exp_pos":7.0,"p_gain":0.4}]}""";

        var response = await _client.SystemOneAsync(JevTestRequests.Strategy(state), CancellationToken.None);

        Assert.Equal("STAY_OUT", response.Answers["pit_call"].Choice);
        Assert.Equal("HARD", response.Answers["compound"].Choice);
    }

    [Fact]
    public async Task SystemOneAsync_TriageWithTextState_AnswersFirstOptionAndScore()
    {
        var response = await _client.SystemOneAsync(JevTestRequests.Triage("SAFETY CAR DEPLOYED"), CancellationToken.None);

        Assert.Equal("NONE", response.Answers["impact"].Choice);
        Assert.Equal(1, response.Answers["urgency"].Score);
        Assert.Null(response.Answers["urgency"].Choice);
    }

    [Fact]
    public void From_StrategyOutcome_RoundsLikeDocumentedState()
    {
        var entry = JevMonteCarloEntry.From(Outcome("BOX_L+1_INTERMEDIATE", PitCall.Box, 61, Compound.Intermediate, 12.437, pGain: 0.4912));

        Assert.Equal(new JevMonteCarloEntry("BOX_L+1_INTERMEDIATE", 12.4, 0.49), entry);
    }

    private static StrategyOutcome Outcome(
        string id,
        PitCall call,
        int? boxLap,
        Compound? compound,
        double expectedPosition,
        double pGain = 0.5) => new(
        id,
        call,
        boxLap,
        compound,
        expectedPosition,
        new Dictionary<int, double> { [(int)Math.Round(expectedPosition)] = 1.0 },
        pGain,
        1 - pGain,
        0.0);
}
