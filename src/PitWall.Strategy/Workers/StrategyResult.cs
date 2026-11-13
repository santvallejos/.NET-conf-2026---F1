using PitWall.Domain;

namespace PitWall.Strategy.Workers;

/// <summary>Estrategia calculada de un piloto en una vuelta, con lo que costó calcularla.</summary>
/// <param name="Iterations">Iteraciones de Monte Carlo por candidata.</param>
/// <param name="Duration">Tiempo total del cálculo del piloto (modelos, reglas y Monte Carlo).</param>
/// <param name="MonteCarloDuration">Tiempo de Monte Carlo (todas las candidatas).</param>
public sealed record StrategyResult(
    StrategySnapshot Snapshot,
    int Iterations,
    TimeSpan Duration,
    TimeSpan MonteCarloDuration);
