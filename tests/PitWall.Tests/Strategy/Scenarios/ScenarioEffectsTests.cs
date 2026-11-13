using PitWall.Domain;
using PitWall.Strategy.MonteCarlo;
using PitWall.Strategy.Rules;
using PitWall.Strategy.Scenarios;
using PitWall.Tests.Strategy.MonteCarlo;

namespace PitWall.Tests.Strategy.Scenarios;

public sealed class ScenarioEffectsTests
{
    private static readonly RaceState State = MonteCarloFixtures.Race(ScenarioFixtures.Lap).State;

    [Fact]
    public void Resolve_RealRace_NoneAndNotHypothetical()
    {
        var effects = ScenarioEffects.Resolve(null, [ScenarioFixtures.Event(InjectedEventType.SafetyCar)], 20, new Dictionary<int, int>(), ScenarioFixtures.Options());

        Assert.Same(ScenarioEffects.None, effects);
        Assert.False(effects.Hypothetical);
        Assert.Same(State, effects.Apply(State));
    }

    [Theory]
    [InlineData(40, Neutralisation.SafetyCar, 4)]
    [InlineData(42, Neutralisation.SafetyCar, 2)]
    [InlineData(43, Neutralisation.SafetyCar, 1)]
    [InlineData(44, Neutralisation.None, 0)]
    [InlineData(39, Neutralisation.None, 0)]
    public void Resolve_SafetyCarAtLap40_ActiveForItsDefaultFourLaps(int raceLap, Neutralisation expected, int lapsLeft)
    {
        var effects = ScenarioFixtures.Effects(State, raceLap, ScenarioFixtures.Event(InjectedEventType.SafetyCar, lap: 40));

        Assert.True(effects.Hypothetical);
        Assert.Equal(expected, effects.Neutralisation);
        Assert.Equal(lapsLeft, effects.NeutralisationLapsLeft);
        Assert.Equal(expected == Neutralisation.None ? 1.0 : 0.5, effects.NeutralisationPitLossFactor);
    }

    [Fact]
    public void Resolve_RainSecondLap_PreviousRainAndLapsLeft()
    {
        var rain = ScenarioFixtures.Event(InjectedEventType.Rain, new InjectedEventParameters { Intensity = RainIntensity.Heavy, DurationLaps = 3 }, lap: 60);

        var first = ScenarioFixtures.Effects(State, 60, rain);
        var second = ScenarioFixtures.Effects(State, 61, rain);
        var after = ScenarioFixtures.Effects(State, 63, rain);

        Assert.Equal((RainIntensity.Heavy, RainIntensity.None, 3), (first.Rain, first.PreviousRain, first.RainLapsLeft));
        Assert.Equal((RainIntensity.Heavy, RainIntensity.Heavy, 2), (second.Rain, second.PreviousRain, second.RainLapsLeft));
        Assert.Equal((RainIntensity.None, RainIntensity.Heavy, 0), (after.Rain, after.PreviousRain, after.RainLapsLeft));
    }

    [Fact]
    public void Resolve_PunctureThenPitStop_RepairedOnNewStint()
    {
        var puncture = ScenarioFixtures.Event(InjectedEventType.Puncture, lap: 15);
        var options = ScenarioFixtures.Options();

        var beforeStop = ScenarioEffects.Resolve(ScenarioFixtures.ScenarioId, [puncture], 20, new Dictionary<int, int> { [MonteCarloFixtures.Focus] = 10 }, options);
        var afterStop = ScenarioEffects.Resolve(ScenarioFixtures.ScenarioId, [puncture], 20, new Dictionary<int, int> { [MonteCarloFixtures.Focus] = 16 }, options);

        Assert.Contains(MonteCarloFixtures.Focus, beforeStop.PuncturedDrivers);
        Assert.Equal(20.0, beforeStop.PunctureLossMinSeconds);
        Assert.Equal(40.0, beforeStop.PunctureLossMaxSeconds);
        Assert.Empty(afterStop.PuncturedDrivers);
        Assert.Empty(afterStop.ActiveEvents);
    }

    [Fact]
    public void Resolve_WingDamageAndErsFailure_PenaltiesFromScenarioOptions()
    {
        var effects = ScenarioFixtures.Effects(
            State,
            20,
            ScenarioFixtures.Event(InjectedEventType.WingDamage, new InjectedEventParameters { Severity = DamageSeverity.Major }, id: 1),
            ScenarioFixtures.Event(InjectedEventType.ErsFailure, new InjectedEventParameters { Severity = DamageSeverity.Minor }, id: 2));

        Assert.Equal(DamageSeverity.Major, effects.WingDamage[MonteCarloFixtures.Focus]);
        Assert.Equal(1.5, effects.RepairablePacePenalties[MonteCarloFixtures.Focus]);
        Assert.Equal(DamageSeverity.Minor, effects.ErsFailures[MonteCarloFixtures.Focus]);
        Assert.Equal(0.5, effects.PermanentPacePenalties[MonteCarloFixtures.Focus]);
        Assert.Equal(2, effects.ActiveEvents.Count);
    }

    [Fact]
    public void Resolve_RivalCrash_IncidentAndSafetyCarOnlyOnItsLapRivalOutForGood()
    {
        var crash = ScenarioFixtures.Event(InjectedEventType.RivalCrash, new InjectedEventParameters { Rival = ScenarioFixtures.CarAhead }, lap: 20);

        var now = ScenarioFixtures.Effects(State, 20, crash);
        var later = ScenarioFixtures.Effects(State, 25, crash);

        Assert.Contains(MonteCarloFixtures.Focus, now.RivalIncidentDrivers);
        Assert.Equal(0.3, now.SafetyCarProbability);
        Assert.Empty(later.RivalIncidentDrivers);
        Assert.Null(later.SafetyCarProbability);
        Assert.Contains(ScenarioFixtures.CarAhead, later.RetiredDrivers);
    }

    [Fact]
    public void Apply_RivalCrash_RemovesCarRenumbersAndAddsInterval()
    {
        var effects = ScenarioFixtures.Effects(State, 20, ScenarioFixtures.Event(InjectedEventType.RivalCrash, new InjectedEventParameters { Rival = ScenarioFixtures.CarAhead }));

        var hypothetical = effects.Apply(State);

        Assert.NotSame(State, hypothetical);
        Assert.Equal(State.Cars.Count - 1, hypothetical.Cars.Count);
        var focus = hypothetical.Cars.Single(car => car.DriverNumber == MonteCarloFixtures.Focus);
        Assert.Equal(4, focus.Position);
        Assert.Equal(3.0, focus.Interval);
        Assert.Equal(Enumerable.Range(1, hypothetical.Cars.Count), hypothetical.Cars.Select(car => car.Position));
        Assert.Equal(hypothetical, effects.Apply(hypothetical));
        Assert.Equal(20, State.Cars.Count);
    }

    [Fact]
    public void Apply_LeaderCrash_NextCarLeadsWithGapsShifted()
    {
        var effects = ScenarioFixtures.Effects(State, 20, ScenarioFixtures.Event(InjectedEventType.RivalCrash, new InjectedEventParameters { Rival = 1 }));

        var cars = effects.Apply(State).Cars;

        Assert.Equal((2, 1, (double?)null), (cars[0].DriverNumber, cars[0].Position, cars[0].GapToLeader));
        Assert.Equal(1.5, cars[1].GapToLeader);
    }

    [Fact]
    public void Apply_SafetyCarRedFlagAndRain_HypotheticalTrackAndWeatherReplayUntouched()
    {
        var effects = ScenarioFixtures.Effects(
            State,
            20,
            ScenarioFixtures.Event(InjectedEventType.SafetyCar, id: 1),
            ScenarioFixtures.Event(InjectedEventType.RedFlag, id: 2),
            ScenarioFixtures.Event(InjectedEventType.Rain, id: 3));

        var hypothetical = effects.Apply(State);

        Assert.Equal(new TrackStatus(ScenarioEffects.RedFlagName, SafetyCar: true, VirtualSafetyCar: false), hypothetical.Track);
        Assert.True(hypothetical.Weather.Rainfall);
        Assert.Equal(new TrackStatus("GREEN", false, false), State.Track);
        Assert.False(State.Weather.Rainfall);
    }

    [Fact]
    public void Apply_DefaultsFromScenarioOptions()
    {
        var options = ScenarioFixtures.Options();

        Assert.Equal(4, InjectedEventDefaults.Apply(InjectedEventType.SafetyCar, new(), options, 43).DurationLaps);
        Assert.Equal(2, InjectedEventDefaults.Apply(InjectedEventType.VirtualSafetyCar, new(), options, 43).DurationLaps);
        Assert.Equal(new InjectedEventParameters(), InjectedEventDefaults.Apply(InjectedEventType.RedFlag, new() { DurationLaps = 3, Driver = 1 }, options, 43));
        Assert.Equal(new InjectedEventParameters { Intensity = RainIntensity.Light, DurationLaps = 10 }, InjectedEventDefaults.Apply(InjectedEventType.Rain, new(), options, 43));
        Assert.Equal(new InjectedEventParameters { Driver = 43 }, InjectedEventDefaults.Apply(InjectedEventType.Puncture, new(), options, 43));
        Assert.Equal(new InjectedEventParameters { Driver = 44 }, InjectedEventDefaults.Apply(InjectedEventType.Puncture, new() { Driver = 44 }, options, 43));
        Assert.Equal(new InjectedEventParameters { Severity = DamageSeverity.Minor, Driver = 43 }, InjectedEventDefaults.Apply(InjectedEventType.WingDamage, new(), options, 43));
        Assert.Equal(new InjectedEventParameters { Severity = DamageSeverity.Major, Driver = 43 }, InjectedEventDefaults.Apply(InjectedEventType.ErsFailure, new() { Severity = DamageSeverity.Major }, options, 43));
        Assert.Equal(new InjectedEventParameters { Driver = 43, Rival = 4, Relative = RivalRelative.Ahead }, InjectedEventDefaults.Apply(InjectedEventType.RivalCrash, new() { Rival = 4 }, options, 43));
        Assert.Throws<ArgumentException>(() => InjectedEventDefaults.Apply(InjectedEventType.RivalCrash, new(), options, 43));
    }

    [Theory]
    [InlineData(RivalRelative.Ahead, 4)]
    [InlineData(RivalRelative.Behind, 6)]
    public void RivalOf_FocusInP5_AdjacentCar(RivalRelative relative, int expected)
    {
        Assert.Equal(expected, InjectedEventDefaults.RivalOf(State, MonteCarloFixtures.Focus, relative));
    }

    [Fact]
    public void Describe_CompactTextForJev()
    {
        Assert.Equal("SAFETY_CAR lap 20 for 4 laps", ScenarioFixtures.Event(InjectedEventType.SafetyCar).Describe());
        Assert.Equal(
            "RAIN HEAVY lap 20 for 10 laps",
            ScenarioFixtures.Event(InjectedEventType.Rain, new InjectedEventParameters { Intensity = RainIntensity.Heavy }).Describe());
        Assert.Equal(
            "RIVAL_CRASH car 4 AHEAD of car 43 lap 20",
            ScenarioFixtures.Event(InjectedEventType.RivalCrash, new InjectedEventParameters { Rival = 4 }).Describe());
        Assert.Equal(
            "ERS_FAILURE MAJOR car 43 lap 20",
            ScenarioFixtures.Event(InjectedEventType.ErsFailure, new InjectedEventParameters { Severity = DamageSeverity.Major }).Describe());
    }
}
