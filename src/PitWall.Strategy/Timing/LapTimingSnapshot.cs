namespace PitWall.Strategy.Timing;

/// <summary>Timing de la sesión con las vueltas completas a un instante.</summary>
/// <param name="CompletedLaps">Vueltas completas de toda la parrilla.</param>
/// <param name="Drivers">Por número de piloto; solo pilotos con al menos una vuelta completa.</param>
public sealed record LapTimingSnapshot(
    int CompletedLaps,
    IReadOnlyDictionary<int, DriverTiming> Drivers,
    TimingRankings Rankings);
