namespace PitWall.Domain;

/// <summary>
/// Modelo de degradación por (piloto, compuesto) de docs/strategy-engine.md §1:
/// <c>lap_time = base + deg · tyre_age + fuel_coef · laps_remaining + ε</c>.
/// </summary>
/// <param name="ResidualSigma">Desvío residual ε de la regresión (0 si no hay regresión).</param>
/// <param name="LapsToCliff">Vueltas hasta superar <c>CliffDeltaSeconds</c> respecto a goma nueva.</param>
/// <param name="IsFallback">True si se usó la media de parrilla por tener menos de 4 vueltas limpias.</param>
public sealed record TyreModel(
    Compound Compound,
    int TyreAge,
    double BaseLapTime,
    double DegPerLap,
    double ResidualSigma,
    int LapsToCliff,
    bool IsFallback);
