using PitWall.Domain;
using PitWall.Jev;
using PitWall.Jev.Decisions;
using PitWall.Jev.Requests;
using PitWall.Jev.Serialization;

namespace PitWall.Tests.Jev;

public sealed class JevDecisionMapperTests
{
    private static readonly Guid DecisionId = new("0193a0b4-0000-7000-8000-000000000001");

    private static readonly IReadOnlyList<Reason> RulesReasons = [Reason.RainStarting, Reason.TyreCliffNear, Reason.LowGrip];

    [Fact]
    public void Map_StrategyResponse_ChoicesConfidenceMinimumAndRulesReasons()
    {
        var request = JevRequestFactory.Strategy("jev-latest", JevTestRace.StrategyInputLap60());
        var response = new JevResponse(JevSerializer.DeserializeAnswers(JevFixtures.Read("strategy_response_lap60.json")))
        {
            LatencyMs = 212,
            Tokens = 640,
            CostUsd = 0.0000269m,
        };

        var decision = JevDecisionMapper.Map(Input(JevTrigger.EveryLap), request, response);

        Assert.NotNull(decision);
        Assert.Equal(DecisionId, decision.Id);
        Assert.Equal(JevKind.Strategy, decision.Kind);
        Assert.Equal(JevTrigger.EveryLap, decision.Trigger);
        Assert.Equal(PitCall.Box, decision.PitCall);
        Assert.Equal(Compound.Intermediate, decision.Compound);
        Assert.Equal(PaceMode.Manage, decision.PaceMode);
        Assert.Equal(EnergyMode.Balanced, decision.EnergyMode);
        Assert.Null(decision.Impact);
        Assert.Null(decision.Urgency);

        // Mínimo de 0.84, 0.9, 0.7 y 0.75.
        Assert.Equal(0.7, decision.Confidence);
        Assert.Equal(RulesReasons, decision.Reasons);
        Assert.Equal(0.84, decision.Probabilities[JevQuestionKeys.PitCall]["BOX"]);
        Assert.Equal(4, decision.Probabilities.Count);
        Assert.False(decision.Hypothetical);
        Assert.False(decision.FromCache);
        Assert.Equal(212, decision.LatencyMs);
        Assert.Equal(640, decision.Tokens);
        Assert.Equal(0.0000269m, decision.CostUsd);
        Assert.Equal(60, decision.Lap);
        Assert.Equal(JevTestRace.FocusDriver, decision.DriverNumber);
    }

    [Fact]
    public void Map_TriageActNow_ImpactAndRoundedUrgency()
    {
        var request = TriageRequest();
        var response = new JevResponse(new Dictionary<string, JevAnswer>
        {
            [JevQuestionKeys.Impact] = new("ACT_NOW", null, new Dictionary<string, double> { ["NONE"] = 0.05, ["MONITOR"] = 0.1, ["ACT_NOW"] = 0.85 }, 0.85),
            [JevQuestionKeys.Urgency] = new(null, 4.6, null, 0.6),
        });

        var decision = JevDecisionMapper.Map(Input(JevTrigger.Triage), request, response);

        Assert.NotNull(decision);
        Assert.Equal(JevKind.Triage, decision.Kind);
        Assert.Equal(Impact.ActNow, decision.Impact);
        Assert.Equal(5, decision.Urgency);
        Assert.Equal(0.6, decision.Confidence);
        Assert.Null(decision.PitCall);
        Assert.Equal([JevQuestionKeys.Impact], decision.Probabilities.Keys);
    }

    [Theory]
    [InlineData(0.2, 1)]
    [InlineData(2.5, 3)]
    [InlineData(9.0, 5)]
    public void Map_UrgencyOutOfScale_ClampedToOneToFive(double score, int expected)
    {
        var response = new JevResponse(new Dictionary<string, JevAnswer>
        {
            [JevQuestionKeys.Urgency] = new(null, score, null, 0.5),
        });

        var decision = JevDecisionMapper.Map(Input(JevTrigger.Triage), TriageRequest(), response);

        Assert.Equal(expected, decision?.Urgency);
    }

    [Theory]
    [InlineData(JevResponseStatus.Disabled)]
    [InlineData(JevResponseStatus.RequestLimitReached)]
    [InlineData(JevResponseStatus.Failed)]
    public void Map_NotCalled_ReturnsNull(JevResponseStatus status)
    {
        var request = JevRequestFactory.Strategy("jev-latest", JevTestRace.StrategyInputLap60());

        Assert.Null(JevDecisionMapper.Map(Input(JevTrigger.EveryLap), request, JevResponse.NotCalled(status)));
    }

    [Fact]
    public void Map_ChoiceNotOffered_IsDiscarded()
    {
        var redFlag = new Constraint("RED_FLAG", [PitCall.Box], [], null, false);
        var request = JevRequestFactory.Strategy("jev-latest", JevTestRace.StrategyInputLap60(JevTestRace.SnapshotLap60(redFlag)));
        var response = new JevResponse(new Dictionary<string, JevAnswer>
        {
            [JevQuestionKeys.PitCall] = new("BOX", null, null, 0.9),
            [JevQuestionKeys.Compound] = new("HARD", null, null, 0.8),
        });

        var decision = JevDecisionMapper.Map(Input(JevTrigger.EveryLap), request, response);

        Assert.NotNull(decision);
        Assert.Null(decision.PitCall);
        Assert.Equal(Compound.Hard, decision.Compound);
    }

    [Fact]
    public void Map_AnswerForQuestionNotAsked_Ignored()
    {
        var response = new JevResponse(new Dictionary<string, JevAnswer>
        {
            [JevQuestionKeys.Impact] = new("MONITOR", null, null, 0.8),
            ["extra"] = new("X", null, null, 0.1),
        });

        var decision = JevDecisionMapper.Map(Input(JevTrigger.Triage), TriageRequest(), response);

        Assert.Equal(0.8, decision?.Confidence);
    }

    [Fact]
    public void Map_WithScenario_Hypothetical()
    {
        var scenarioId = Guid.NewGuid();
        var response = new JevResponse(new Dictionary<string, JevAnswer>
        {
            [JevQuestionKeys.Impact] = new("NONE", null, null, 0.9),
        })
        {
            FromCache = true,
        };

        var decision = JevDecisionMapper.Map(Input(JevTrigger.Triage) with { ScenarioId = scenarioId }, TriageRequest(), response);

        Assert.NotNull(decision);
        Assert.True(decision.Hypothetical);
        Assert.Equal(scenarioId, decision.ScenarioId);
        Assert.True(decision.FromCache);
    }

    private static JevDecisionInput Input(JevTrigger trigger) =>
        new(DecisionId, JevTestRequests.SessionKey, null, JevTestRace.FocusDriver, 60, trigger, RulesReasons);

    private static JevRequest TriageRequest() => JevRequestFactory.Triage(
        "jev-latest",
        new JevTriageInput(JevTestRequests.SessionKey, JevTestRace.StateLap60(), JevTestRace.FocusDriver, ["SAFETY CAR DEPLOYED"], []));
}
