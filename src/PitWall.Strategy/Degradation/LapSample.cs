using PitWall.Domain;

namespace PitWall.Strategy.Degradation;

/// <summary>Vuelta completada de un piloto, con lo necesario para decidir si es limpia.</summary>
/// <param name="LapTime">Duración en segundos; <c>null</c> si OpenF1 no la registró.</param>
/// <param name="TyreAge">Edad del neumático en la vuelta: <c>tyre_age_at_start + (lap − lap_start)</c>.</param>
/// <param name="IsPitInLap">Vuelta de entrada a boxes.</param>
/// <param name="IsNeutralised">Hubo bandera amarilla, roja, SC o VSC activa durante la vuelta.</param>
public sealed record LapSample(
    int DriverNumber,
    int LapNumber,
    Compound Compound,
    int TyreAge,
    double? LapTime,
    bool IsPitOutLap,
    bool IsPitInLap,
    bool IsNeutralised);
