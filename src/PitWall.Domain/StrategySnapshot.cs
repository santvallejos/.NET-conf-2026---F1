namespace PitWall.Domain;

/// <summary>Resultado de estrategia por piloto y vuelta (docs/architecture.md §4).</summary>
public sealed record StrategySnapshot(
    int DriverNumber,
    int Lap,
    TyreModel Tyre,
    EnergyEstimate Energy)
{
    /// <summary>Pit loss medio (s) de la llamada a boxes: <c>lane_duration</c> de la sesión o el de configuración.</summary>
    public double? PitLossSeconds { get; init; }
}
