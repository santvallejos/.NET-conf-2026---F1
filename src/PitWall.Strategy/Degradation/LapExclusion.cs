namespace PitWall.Strategy.Degradation;

/// <summary>Motivo por el que una vuelta no entra en la regresión; <see cref="None"/> si es limpia.</summary>
public enum LapExclusion
{
    None,
    NoLapTime,
    FirstLap,
    PitOutLap,
    PitInLap,
    Neutralised,
    Outlier,
}
