using PitWall.Domain;
using PitWall.Strategy.Degradation;

namespace PitWall.Strategy.Workers;

/// <summary>Lo que se sabe de la carrera en el instante del cálculo; nada posterior a <see cref="RaceState.Clock"/>.</summary>
/// <param name="CompletedLaps">Vueltas de toda la parrilla cerradas hasta el instante.</param>
/// <param name="UsedCompounds">Compuestos montados por piloto hasta su vuelta actual.</param>
/// <param name="RaceControl">Mensajes de Race Control emitidos hasta el instante.</param>
/// <param name="PreviousRainfall">Llovía una vuelta atrás.</param>
/// <param name="TrackWet">La pista sigue mojada (llovió hace poco).</param>
/// <param name="PitLaneDurations"><c>lane_duration</c> (s) de las paradas de la sesión hasta el instante.</param>
/// <param name="Overtakes">Adelantamientos registrados en la sesión hasta el instante.</param>
/// <param name="Energy">Energía estimada por piloto; el que falta se asume con batería llena (ESTIMADO).</param>
/// <param name="PuncturedDrivers">Pilotos con pinchazo inyectado.</param>
public sealed record StrategyContext(
    RaceState State,
    int TotalLaps,
    IReadOnlyList<LapSample> CompletedLaps,
    IReadOnlyDictionary<int, IReadOnlyList<Compound>> UsedCompounds,
    IReadOnlyList<RaceControlMessage> RaceControl,
    bool PreviousRainfall,
    bool TrackWet,
    IReadOnlyList<double> PitLaneDurations,
    int Overtakes,
    IReadOnlyDictionary<int, EnergyEstimate> Energy,
    IReadOnlySet<int> PuncturedDrivers);
