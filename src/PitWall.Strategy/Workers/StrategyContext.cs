using PitWall.Domain;
using PitWall.Strategy.Degradation;

namespace PitWall.Strategy.Workers;

/// <summary>Lo que se sabe de la carrera en el instante del cálculo; nada posterior a <see cref="RaceState.Clock"/>.</summary>
/// <param name="CompletedLaps">Vueltas de toda la parrilla cerradas hasta el instante.</param>
/// <param name="RaceControl">Mensajes de Race Control emitidos hasta el instante.</param>
/// <param name="PitLaneDurations"><c>lane_duration</c> (s) de las paradas de la sesión hasta el instante.</param>
/// <param name="Energy">Energía estimada por piloto; el que falta se asume con batería llena (ESTIMADO).</param>
public sealed record StrategyContext(
    RaceState State,
    int TotalLaps,
    IReadOnlyList<LapSample> CompletedLaps,
    IReadOnlyList<RaceControlMessage> RaceControl,
    IReadOnlyList<double> PitLaneDurations,
    IReadOnlyDictionary<int, EnergyEstimate> Energy);
