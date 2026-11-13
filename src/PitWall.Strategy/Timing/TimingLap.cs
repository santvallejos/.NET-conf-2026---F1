namespace PitWall.Strategy.Timing;

/// <summary>Vuelta de un piloto tal como la publica OpenF1; tiempos en segundos.</summary>
/// <param name="Duration">Tiempo de vuelta; <c>null</c> si OpenF1 no lo registró (la vuelta nunca cuenta como completa).</param>
/// <param name="IsInLap">Vuelta de entrada a boxes.</param>
public sealed record TimingLap(
    int DriverNumber,
    int LapNumber,
    DateTimeOffset DateStart,
    double? Duration,
    double? S1,
    double? S2,
    double? S3,
    bool IsPitOutLap,
    bool IsInLap);
