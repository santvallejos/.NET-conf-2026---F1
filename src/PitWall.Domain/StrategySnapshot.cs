namespace PitWall.Domain;

/// <summary>Resultado de estrategia por piloto y vuelta; input de Jev (docs/architecture.md §4).</summary>
public sealed record StrategySnapshot(
    int DriverNumber,
    int Lap,
    TyreModel Tyre,
    EnergyEstimate Energy,
    IReadOnlyList<StrategyOutcome> Outcomes,
    IReadOnlyList<Reason> Reasons,
    IReadOnlyList<Constraint> Constraints);
