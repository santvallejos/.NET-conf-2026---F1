using PitWall.Domain;

namespace PitWall.Strategy.MonteCarlo;

/// <summary>Auto de la parrilla al empezar la proyección.</summary>
/// <param name="GapToLeader">Segundos al líder (0 para el líder).</param>
/// <param name="Tyre">Modelo de degradación del stint actual; su edad es la de la vuelta en curso.</param>
/// <param name="MandatoryStopPending">Aún debe montar un segundo compuesto seco antes del final.</param>
public sealed record MonteCarloCar(
    int DriverNumber,
    double GapToLeader,
    TyreModel Tyre,
    bool MandatoryStopPending);
