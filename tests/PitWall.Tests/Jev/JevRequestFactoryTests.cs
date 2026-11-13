using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

using PitWall.Domain;
using PitWall.Jev;
using PitWall.Jev.Requests;
using PitWall.Jev.Serialization;
using PitWall.Strategy.Rules;
using PitWall.Tests.Strategy.Rules;

namespace PitWall.Tests.Jev;

public sealed class JevRequestFactoryTests
{
    private static readonly RulesEngine Rules = new(new RulesOptions());

    public static TheoryData<string> RuleScenarios => ["neutral", "red_flag", "pit_lane_closed", "heavy_rain", "puncture", "puncture_red_flag", "wing_damage"];

    [Fact]
    public void Strategy_Lap60_MatchesDocumentedRequestFixture()
    {
        var expected = JsonNode.Parse(JevFixtures.Read("strategy_request_lap60.json"))!
            .ToJsonString(new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

        var request = JevRequestFactory.Strategy("jev-latest", JevTestRace.StrategyInputLap60());

        Assert.Equal(expected, JevSerializer.SerializeRequest(request));
        Assert.Equal(JevKind.Strategy, request.Kind);
        Assert.Equal(JevTestRequests.SessionKey, request.SessionKey);
    }

    [Fact]
    public void Strategy_AnySnapshot_AsksTheFourQuestions()
    {
        var request = JevRequestFactory.Strategy("jev-latest", JevTestRace.StrategyInputLap60());

        Assert.Equal(
            [JevQuestionKeys.Compound, JevQuestionKeys.EnergyMode, JevQuestionKeys.PaceMode, JevQuestionKeys.PitCall],
            request.Questions.Keys.Order(StringComparer.Ordinal));
        Assert.All(request.Questions.Values, question => Assert.Equal(JevQuestionType.Choice, question.Type));
    }

    [Theory]
    [MemberData(nameof(RuleScenarios))]
    public void Strategy_RulesEngineConstraints_OnlyAllowedOptionsAreSent(string scenario)
    {
        var constraints = Rules.EvaluateConstraints(Features(scenario));
        var snapshot = JevTestRace.SnapshotLap60([.. constraints]);

        var request = JevRequestFactory.Strategy("jev-latest", JevTestRace.StrategyInputLap60(snapshot));

        var expectedCalls = Enum.GetValues<PitCall>().Where(call => RulesEngine.IsAllowed(constraints, call, null)).ToList();
        var expectedCompounds = Enum.GetValues<Compound>().Where(compound => RulesEngine.IsAllowed(constraints, expectedCalls[0], compound));
        Assert.Equal(JevContractNames.Of(expectedCalls), request.Questions[JevQuestionKeys.PitCall].Criteria);
        Assert.Equal(JevContractNames.Of(expectedCompounds), request.Questions[JevQuestionKeys.Compound].Criteria);
    }

    [Fact]
    public void Strategy_RedFlag_BoxNotSent()
    {
        var constraints = Rules.EvaluateConstraints(Features("red_flag"));

        var request = JevRequestFactory.Strategy("jev-latest", JevTestRace.StrategyInputLap60(JevTestRace.SnapshotLap60([.. constraints])));

        Assert.Equal(["STAY_OUT", "BOX_NEXT_LAP"], request.Questions[JevQuestionKeys.PitCall].Criteria);
    }

    [Fact]
    public void Strategy_Puncture_OnlyBoxSent()
    {
        var constraints = Rules.EvaluateConstraints(Features("puncture"));

        var request = JevRequestFactory.Strategy("jev-latest", JevTestRace.StrategyInputLap60(JevTestRace.SnapshotLap60([.. constraints])));

        Assert.Equal(["BOX"], request.Questions[JevQuestionKeys.PitCall].Criteria);
    }

    [Fact]
    public void Strategy_DryTrack_WetNotSent()
    {
        var constraints = Rules.EvaluateConstraints(Features("neutral"));

        var request = JevRequestFactory.Strategy("jev-latest", JevTestRace.StrategyInputLap60(JevTestRace.SnapshotLap60([.. constraints])));

        Assert.Equal(["SOFT", "MEDIUM", "HARD", "INTERMEDIATE"], request.Questions[JevQuestionKeys.Compound].Criteria);
    }

    [Fact]
    public void Strategy_HeavyRain_DryCompoundsNotSent()
    {
        var constraints = Rules.EvaluateConstraints(Features("heavy_rain"));

        var request = JevRequestFactory.Strategy("jev-latest", JevTestRace.StrategyInputLap60(JevTestRace.SnapshotLap60([.. constraints])));

        Assert.Equal(["INTERMEDIATE", "WET"], request.Questions[JevQuestionKeys.Compound].Criteria);
    }

    [Fact]
    public void Strategy_TopOutcomes_LimitsMonteCarloInState()
    {
        var request = JevRequestFactory.Strategy("jev-latest", JevTestRace.StrategyInputLap60(topOutcomes: 2));

        var mc = JsonNode.Parse(request.State)!["mc"]!.AsArray();
        Assert.Equal(["STAY_OUT", "BOX_L_INTERMEDIATE"], mc.Select(entry => entry!["id"]!.GetValue<string>()));
    }

    [Fact]
    public void Strategy_TyreWithoutCliff_OmitsLapsToCliff()
    {
        var snapshot = JevTestRace.SnapshotLap60() with
        {
            Tyre = JevTestRace.SnapshotLap60().Tyre with { LapsToCliff = int.MaxValue },
        };

        var request = JevRequestFactory.Strategy("jev-latest", JevTestRace.StrategyInputLap60(snapshot));

        var tyre = JsonNode.Parse(request.State)!["tyre"]!.AsObject();
        Assert.False(tyre.ContainsKey("laps_to_cliff"));
        Assert.Equal(0.11, tyre["deg_s_per_lap"]!.GetValue<double>());
    }

    [Fact]
    public void Strategy_Leader_OmitsGapAhead()
    {
        var input = JevTestRace.StrategyInputLap60();
        var state = input.State with
        {
            Cars = [.. input.State.Cars.Select(car => car.DriverNumber == JevTestRace.FocusDriver ? car with { Position = 1, Interval = null } : car)],
        };

        var request = JevRequestFactory.Strategy("jev-latest", input with { State = state });

        var json = JsonNode.Parse(request.State)!.AsObject();
        Assert.False(json.ContainsKey("gap_ahead"));
        Assert.False(json.ContainsKey("gap_behind"));
        Assert.Equal(1, json["pos"]!.GetValue<int>());
    }

    [Fact]
    public void Strategy_DriverNotInState_Throws()
    {
        var snapshot = JevTestRace.SnapshotLap60() with { DriverNumber = 99 };

        Assert.Throws<ArgumentException>(() => JevRequestFactory.Strategy("jev-latest", JevTestRace.StrategyInputLap60(snapshot)));
    }

    [Fact]
    public void Triage_NewMessages_ImpactAndUrgencyWithMinimalSummary()
    {
        var request = JevRequestFactory.Triage(
            "jev-latest",
            new JevTriageInput(JevTestRequests.SessionKey, JevTestRace.StateLap60(), JevTestRace.FocusDriver, ["SAFETY CAR DEPLOYED"], []));

        Assert.Equal(JevKind.Triage, request.Kind);
        Assert.Equal(["NONE", "MONITOR", "ACT_NOW"], request.Questions[JevQuestionKeys.Impact].Criteria);
        Assert.Equal(JevQuestionType.Score, request.Questions[JevQuestionKeys.Urgency].Type);
        Assert.Equal(2, request.Questions.Count);
        Assert.Equal(
            """{"lap":60,"pos":13,"tyre":{"compound":"SOFT","age":42},"weather":{"rain":true,"track_c":29.1,"humidity":81},"track":{"flag":"GREEN","sc":false,"vsc":false},"race_control":["SAFETY CAR DEPLOYED"],"injected":[]}""",
            request.State);
    }

    [Fact]
    public void Triage_InjectedEvent_TravelsInInjected()
    {
        var request = JevRequestFactory.Triage(
            "jev-latest",
            new JevTriageInput(JevTestRequests.SessionKey, JevTestRace.StateLap60(), JevTestRace.FocusDriver, [], ["SAFETY_CAR"]));

        Assert.Equal(["SAFETY_CAR"], JsonNode.Parse(request.State)!["injected"]!.AsArray().Select(node => node!.GetValue<string>()));
    }

    private static RuleFeatures Features(string scenario)
    {
        var neutral = RuleFeaturesFactory.Neutral();
        return scenario switch
        {
            "red_flag" => neutral with { Track = new TrackStatus("RED", false, false) },
            "pit_lane_closed" => neutral with { PitLaneClosed = true },
            "heavy_rain" => neutral with { Rain = RainIntensity.Heavy, TrackWet = true },
            "puncture" => neutral with { Puncture = true },
            "puncture_red_flag" => neutral with { Puncture = true, Track = new TrackStatus("RED", false, false) },
            "wing_damage" => neutral with { WingDamage = DamageSeverity.Major },
            _ => neutral,
        };
    }
}
