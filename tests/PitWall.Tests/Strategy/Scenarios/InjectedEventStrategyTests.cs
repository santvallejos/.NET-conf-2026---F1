using PitWall.Domain;
using PitWall.Jev.Requests;
using PitWall.Strategy.MonteCarlo;
using PitWall.Strategy.Rules;
using PitWall.Strategy.Scenarios;
using PitWall.Tests.Strategy.MonteCarlo;

namespace PitWall.Tests.Strategy.Scenarios;

/// <summary>
/// Un test por evento del catálogo (docs/scenarios.md §3): cada uno modifica Monte Carlo y/o las reglas, genera su
/// reason y el resultado queda marcado hipotético. Carrera sintética en la vuelta 20 de 70, 1000 iteraciones y
/// semilla fija.
/// </summary>
public sealed class InjectedEventStrategyTests
{
    private const int Lap = ScenarioFixtures.Lap;

    private static readonly Lazy<StrategySnapshot> Real = new(() => ScenarioFixtures.Calculate(MonteCarloFixtures.Race(Lap)));

    [Fact]
    public void Calculate_RealRace_NotHypothetical()
    {
        Assert.False(Real.Value.Hypothetical);
    }

    [Fact]
    public void Calculate_SafetyCar_ScDeployedAndCheaperStop()
    {
        var snapshot = ScenarioFixtures.Calculate(ScenarioFixtures.Context(ScenarioFixtures.Event(InjectedEventType.SafetyCar)));

        Assert.True(snapshot.Hypothetical);
        Assert.Contains(Reason.ScDeployed, snapshot.Reasons);
        // Pit loss × 0,5 bajo SC: parar ahora cuesta menos respecto de seguir en pista (aunque se sale en un pelotón
        // agrupado) y se pierden menos posiciones.
        Assert.True(
            ScenarioFixtures.CostVersusStayOut(snapshot, "BOX_L_HARD") < ScenarioFixtures.CostVersusStayOut(Real.Value, "BOX_L_HARD") - 2,
            "A stop under the injected safety car must be cheaper than under green.");
        Assert.True(ScenarioFixtures.Outcome(snapshot, "BOX_L_HARD").ExpectedPosition < ScenarioFixtures.Outcome(Real.Value, "BOX_L_HARD").ExpectedPosition);
    }

    [Fact]
    public void Calculate_VirtualSafetyCar_VscDeployedAndCheaperStop()
    {
        var snapshot = ScenarioFixtures.Calculate(ScenarioFixtures.Context(ScenarioFixtures.Event(InjectedEventType.VirtualSafetyCar)));

        Assert.True(snapshot.Hypothetical);
        Assert.Contains(Reason.VscDeployed, snapshot.Reasons);
        Assert.DoesNotContain(Reason.ScDeployed, snapshot.Reasons);
        // Pit loss × 0,65 bajo VSC: ~7,7 s menos.
        Assert.True(
            ScenarioFixtures.CostVersusStayOut(snapshot, "BOX_L_HARD") < ScenarioFixtures.CostVersusStayOut(Real.Value, "BOX_L_HARD") - 3,
            "A stop under the injected VSC must be cheaper than under green.");
    }

    [Fact]
    public void Calculate_RedFlag_RedFlagReasonAndNoBoxNow()
    {
        var snapshot = ScenarioFixtures.Calculate(ScenarioFixtures.Context(ScenarioFixtures.Event(InjectedEventType.RedFlag)));

        Assert.True(snapshot.Hypothetical);
        Assert.Contains(Reason.RedFlag, snapshot.Reasons);
        Assert.Contains(snapshot.Constraints, constraint => constraint.Rule == ConstraintRules.RedFlag);
        Assert.NotEmpty(snapshot.Outcomes);
        Assert.DoesNotContain(snapshot.Outcomes, outcome => outcome.Call == PitCall.Box);
        // Cambio de gomas libre en parrilla: una parada extra después ya no gana goma nueva y cuesta más que en verde.
        Assert.True(
            ScenarioFixtures.CostVersusStayOut(snapshot, "BOX_L+1_HARD") > ScenarioFixtures.CostVersusStayOut(Real.Value, "BOX_L+1_HARD"),
            "After the free tyre change of a red flag an extra stop must cost more.");
    }

    [Fact]
    public void Calculate_HeavyRain_RainStartingNoDryCompoundsAndDryPacePenalised()
    {
        var rain = ScenarioFixtures.Event(InjectedEventType.Rain, new InjectedEventParameters { Intensity = RainIntensity.Heavy, DurationLaps = 10 });

        var snapshot = ScenarioFixtures.Calculate(ScenarioFixtures.Context(rain));

        Assert.True(snapshot.Hypothetical);
        Assert.Contains(Reason.RainStarting, snapshot.Reasons);
        Assert.Contains(snapshot.Constraints, constraint => constraint.Rule == ConstraintRules.HeavyRain);
        Assert.DoesNotContain(snapshot.Outcomes, outcome => outcome.Compound is Compound.Soft or Compound.Medium or Compound.Hard);
        // Con secos bajo lluvia cierta el foco pierde posiciones frente a la carrera real.
        Assert.True(
            ScenarioFixtures.Outcome(snapshot, "STAY_OUT").ExpectedPosition > ScenarioFixtures.Outcome(Real.Value, "STAY_OUT").ExpectedPosition + 1,
            "Dry tyres in heavy rain must cost positions.");
    }

    [Fact]
    public void Calculate_Puncture_PunctureReasonAndOnlyBoxNow()
    {
        var snapshot = ScenarioFixtures.Calculate(ScenarioFixtures.Context(ScenarioFixtures.Event(InjectedEventType.Puncture)));

        Assert.True(snapshot.Hypothetical);
        Assert.Contains(Reason.Puncture, snapshot.Reasons);
        Assert.Contains(snapshot.Constraints, constraint => constraint.Rule == ConstraintRules.Puncture);
        Assert.NotEmpty(snapshot.Outcomes);
        Assert.All(snapshot.Outcomes, outcome =>
        {
            Assert.Equal(PitCall.Box, outcome.Call);
            Assert.Equal(Lap, outcome.BoxLap);
        });
        // Vuelta lenta de 20–40 s hasta boxes.
        Assert.True(
            ScenarioFixtures.Outcome(snapshot, "BOX_L_HARD").ExpectedPosition > ScenarioFixtures.Outcome(Real.Value, "BOX_L_HARD").ExpectedPosition,
            "The slow lap with a puncture must cost positions.");
    }

    [Fact]
    public void Calculate_MajorWingDamage_WingDamageReasonForcedBoxAndSlowerPace()
    {
        var major = ScenarioFixtures.Calculate(ScenarioFixtures.Context(
            ScenarioFixtures.Event(InjectedEventType.WingDamage, new InjectedEventParameters { Severity = DamageSeverity.Major })));
        var minor = ScenarioFixtures.Calculate(ScenarioFixtures.Context(
            ScenarioFixtures.Event(InjectedEventType.WingDamage, new InjectedEventParameters { Severity = DamageSeverity.Minor })));

        Assert.True(major.Hypothetical);
        Assert.Contains(Reason.WingDamage, major.Reasons);
        Assert.Contains(major.Constraints, constraint => constraint.Rule == ConstraintRules.WingDamage);
        Assert.All(major.Outcomes, outcome => Assert.Equal(PitCall.Box, outcome.Call));

        // Leve: no fuerza la parada, pero −0,3 s/vuelta hasta parar.
        Assert.Contains(Reason.WingDamage, minor.Reasons);
        Assert.DoesNotContain(minor.Constraints, constraint => constraint.Rule == ConstraintRules.WingDamage);
        Assert.True(
            ScenarioFixtures.Outcome(minor, "STAY_OUT").ExpectedPosition > ScenarioFixtures.Outcome(Real.Value, "STAY_OUT").ExpectedPosition,
            "Minor wing damage must cost positions when staying out.");
    }

    [Fact]
    public void Calculate_RivalCrashAhead_RivalIncidentAndOnePositionLess()
    {
        var crash = ScenarioFixtures.Event(InjectedEventType.RivalCrash, new InjectedEventParameters { Rival = ScenarioFixtures.CarAhead, Relative = RivalRelative.Ahead });
        var context = ScenarioFixtures.Context(crash);

        var snapshot = ScenarioFixtures.Calculate(context);

        Assert.True(snapshot.Hypothetical);
        Assert.Contains(Reason.RivalIncident, snapshot.Reasons);
        Assert.DoesNotContain(context.Scenario.Apply(context.State).Cars, car => car.DriverNumber == ScenarioFixtures.CarAhead);
        // El rival eliminado deja al foco una posición más adelante.
        Assert.True(
            ScenarioFixtures.Outcome(snapshot, "STAY_OUT").ExpectedPosition < ScenarioFixtures.Outcome(Real.Value, "STAY_OUT").ExpectedPosition - 0.5,
            "Removing the rival ahead must improve the expected position.");
    }

    [Fact]
    public void Calculate_MajorErsFailure_ErsFailureReasonNoDeployOrOvertakeAndSlowerPace()
    {
        var snapshot = ScenarioFixtures.Calculate(ScenarioFixtures.Context(
            ScenarioFixtures.Event(InjectedEventType.ErsFailure, new InjectedEventParameters { Severity = DamageSeverity.Major })));

        Assert.True(snapshot.Hypothetical);
        Assert.Contains(Reason.ErsFailure, snapshot.Reasons);
        Assert.DoesNotContain(Reason.OvertakeAvailable, snapshot.Reasons);
        var ers = Assert.Single(snapshot.Constraints, constraint => constraint.Rule == ConstraintRules.ErsFailure);
        Assert.Equal(new[] { EnergyMode.Deploy, EnergyMode.Overtake }, ers.ForbiddenEnergyModes);
        Assert.Equal(new[] { EnergyMode.Harvest, EnergyMode.Balanced }, JevAllowedOptions.EnergyModes(snapshot.Constraints));
        // −2 s/vuelta hasta el final, también después de parar.
        Assert.True(
            ScenarioFixtures.Outcome(snapshot, "BOX_L_HARD").ExpectedPosition > ScenarioFixtures.Outcome(Real.Value, "BOX_L_HARD").ExpectedPosition + 1,
            "An ERS failure must cost positions even after stopping.");
    }

    [Fact]
    public void Calculate_ScheduledEvent_SameAsRealButHypothetical()
    {
        var scheduled = ScenarioFixtures.Event(InjectedEventType.SafetyCar, lap: Lap + 5);

        var snapshot = ScenarioFixtures.Calculate(ScenarioFixtures.Context(scheduled));

        Assert.True(snapshot.Hypothetical);
        Assert.DoesNotContain(Reason.ScDeployed, snapshot.Reasons);
        Assert.Equal(
            Real.Value.Outcomes.Select(o => (o.StrategyId, o.ExpectedPosition)),
            snapshot.Outcomes.Select(o => (o.StrategyId, o.ExpectedPosition)));
    }

    [Fact]
    public void Run_InjectedSafetyCarPitLossFactor_StopNowCostsHalfThePitLoss()
    {
        var simulator = new MonteCarloSimulator(MonteCarloFixtures.Options());
        var candidates = new[] { MonteCarloFixtures.StayOut(), MonteCarloFixtures.Box(30, Compound.Hard, "BOX_L_HARD") };
        // Solo el foco en pista: sin tráfico, la diferencia es la del pit loss.
        var input = MonteCarloFixtures.Input(cars: 1, focusPosition: 1) with { ActiveNeutralisation = Neutralisation.SafetyCar, ActiveNeutralisationLaps = 4 };

        var full = simulator.Run(input with { ActiveNeutralisationPitLossFactor = 1.0 }, candidates);
        var half = simulator.Run(input with { ActiveNeutralisationPitLossFactor = 0.5 }, candidates);

        // Pit loss de 22 s: con el factor 0,5 del SC inyectado la parada cuesta ~11 s menos.
        Assert.InRange(Cost(full) - Cost(half), 8.0, 14.0);

        static double Cost(IReadOnlyList<StrategyOutcome> outcomes) => outcomes[1].ExpectedRaceTimeDelta - outcomes[0].ExpectedRaceTimeDelta;
    }
}
