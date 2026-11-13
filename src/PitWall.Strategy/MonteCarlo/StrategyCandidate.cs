using PitWall.Domain;

namespace PitWall.Strategy.MonteCarlo;

/// <summary>Estrategia candidata del piloto foco.</summary>
/// <param name="Id">P. ej. <c>STAY_OUT</c>, <c>BOX_L_SOFT</c>, <c>BOX_L+1_INTERMEDIATE</c>.</param>
/// <param name="Call">Lo que se hace en la vuelta en curso: <c>BOX</c>, <c>BOX_NEXT_LAP</c> o seguir en pista.</param>
/// <param name="BoxLap">Vuelta de la parada dentro de la ventana <c>L..L+3</c>; null para <c>STAY_OUT</c>.</param>
/// <param name="Compound">Compuesto a montar en esa parada; null para <c>STAY_OUT</c>.</param>
/// <param name="StopLap">Vuelta en la que se simula la parada (para <c>STAY_OUT</c>, la del cliff); null sin parada.</param>
/// <param name="StopCompound">Compuesto montado en <paramref name="StopLap"/>.</param>
public sealed record StrategyCandidate(
    string Id,
    PitCall Call,
    int? BoxLap,
    Compound? Compound,
    int? StopLap,
    Compound? StopCompound);
