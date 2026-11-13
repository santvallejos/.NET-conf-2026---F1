using PitWall.Domain;
using PitWall.Strategy.Scenarios;
using PitWall.Strategy.Workers;
using PitWall.Tests.Strategy.MonteCarlo;

namespace PitWall.Tests.Strategy.Scenarios;

/// <summary>Eventos inyectados sobre la carrera sintética de <see cref="MonteCarloFixtures.Race"/> (foco #43 en P5).</summary>
internal static class ScenarioFixtures
{
    public const int Lap = 20;

    /// <summary>Auto inmediatamente adelante del foco en la carrera sintética.</summary>
    public const int CarAhead = 4;

    public static readonly Guid ScenarioId = new("0b6f7f4e-2a8c-4d43-9b55-6d1d7e1c4016");

    public static ScenarioOptions Options() => new();

    public static InjectedEvent Event(InjectedEventType type, InjectedEventParameters? parameters = null, int lap = Lap, int id = 1) =>
        new(id, ScenarioId, type, lap, InjectedEventDefaults.Apply(type, parameters ?? new InjectedEventParameters(), Options(), MonteCarloFixtures.Focus), DateTimeOffset.UnixEpoch);

    /// <summary>Contexto de la carrera sintética en <see cref="Lap"/> con los efectos de <paramref name="events"/>.</summary>
    public static StrategyContext Context(params InjectedEvent[] events)
    {
        var context = MonteCarloFixtures.Race(Lap);
        var effects = Effects(context.State, Lap, events);
        return context with { PuncturedDrivers = effects.PuncturedDrivers, Scenario = effects };
    }

    public static ScenarioEffects Effects(RaceState state, int raceLap, params InjectedEvent[] events) =>
        ScenarioEffects.Resolve(ScenarioId, events, raceLap, StintStarts(state), Options());

    public static StrategySnapshot Calculate(StrategyContext context) =>
        MonteCarloFixtures.Calculator().Calculate(context, MonteCarloFixtures.Focus)!.Snapshot;

    public static StrategyOutcome Outcome(StrategySnapshot snapshot, string strategyId) =>
        snapshot.Outcomes.Single(outcome => outcome.StrategyId == strategyId);

    /// <summary>Tiempo medio de carrera de <paramref name="strategyId"/> menos el de <c>STAY_OUT</c>.</summary>
    public static double CostVersusStayOut(StrategySnapshot snapshot, string strategyId) =>
        Outcome(snapshot, strategyId).ExpectedRaceTimeDelta - Outcome(snapshot, "STAY_OUT").ExpectedRaceTimeDelta;

    // La carrera sintética: el foco empezó su stint de medios en la vuelta 10 y el resto en la 2.
    private static Dictionary<int, int> StintStarts(RaceState state) =>
        state.Cars.ToDictionary(car => car.DriverNumber, car => car.DriverNumber == MonteCarloFixtures.Focus ? 10 : 2);
}
