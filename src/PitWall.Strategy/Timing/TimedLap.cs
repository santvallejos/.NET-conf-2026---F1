namespace PitWall.Strategy.Timing;

/// <summary>
/// Vuelta completa con cada sector y el tiempo clasificados contra los mejores vigentes al cerrarla.
/// Un sector sin tiempo queda <c>null</c>.
/// </summary>
public sealed record TimedLap(
    int DriverNumber,
    int LapNumber,
    DateTimeOffset End,
    bool IsPitOutLap,
    bool IsInLap,
    TimedValue? S1,
    TimedValue? S2,
    TimedValue? S3,
    TimedValue Lap);
