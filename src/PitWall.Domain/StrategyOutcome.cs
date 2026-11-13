namespace PitWall.Domain;

/// <summary>Resultado de Monte Carlo para una estrategia candidata (docs/strategy-engine.md §4.3).</summary>
/// <param name="StrategyId">P. ej. <c>"BOX_L+1_INTERMEDIATE"</c>.</param>
/// <param name="PositionDistribution">P(posición final = k).</param>
/// <param name="PGainPositions">P(terminar mejor que ahora).</param>
/// <param name="ExpectedRaceTimeDelta">Delta de tiempo vs. la mejor estrategia.</param>
public sealed record StrategyOutcome(
    string StrategyId,
    PitCall Call,
    int? BoxLap,
    Compound? Compound,
    double ExpectedPosition,
    IReadOnlyDictionary<int, double> PositionDistribution,
    double PGainPositions,
    double PLosePositions,
    double ExpectedRaceTimeDelta);
