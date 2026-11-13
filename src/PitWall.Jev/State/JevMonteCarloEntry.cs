using PitWall.Domain;

namespace PitWall.Jev.State;

/// <summary>Entrada compacta de Monte Carlo dentro del <c>state</c> (<c>mc</c>, docs/jev.md §3.3).</summary>
/// <param name="Id">Id de la estrategia candidata (p. ej. <c>BOX_L+1_INTERMEDIATE</c>).</param>
/// <param name="ExpPos">Posición final esperada.</param>
/// <param name="PGain">Probabilidad de terminar mejor que ahora.</param>
public sealed record JevMonteCarloEntry(string Id, double ExpPos, double PGain)
{
    // Redondeo como en el ejemplo documentado: menos tokens y más aciertos de caché.
    public static JevMonteCarloEntry From(StrategyOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        return new JevMonteCarloEntry(
            outcome.StrategyId,
            Math.Round(outcome.ExpectedPosition, 1),
            Math.Round(outcome.PGainPositions, 2));
    }
}
