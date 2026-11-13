using PitWall.Domain;

namespace PitWall.Strategy.Workers;

/// <summary>Estrategia calculada de un piloto en una vuelta, con lo que costó calcularla.</summary>
/// <param name="Duration">Tiempo total del cálculo del piloto (modelos de gomas y energía, pit loss).</param>
public sealed record StrategyResult(StrategySnapshot Snapshot, TimeSpan Duration);
