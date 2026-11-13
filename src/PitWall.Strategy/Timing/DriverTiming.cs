namespace PitWall.Strategy.Timing;

/// <summary>Timing de un piloto con sus vueltas completas a un instante.</summary>
/// <param name="Laps">Vueltas completas ordenadas por número de vuelta.</param>
/// <param name="IdealLap">Suma de los mejores sectores propios; <c>null</c> si falta alguno.</param>
public sealed record DriverTiming(
    int DriverNumber,
    TimedLap LastLap,
    IReadOnlyList<TimedLap> Laps,
    double? BestS1,
    double? BestS2,
    double? BestS3,
    double BestLap,
    double? IdealLap);
