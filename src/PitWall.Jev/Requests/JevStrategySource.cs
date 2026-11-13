using PitWall.Domain;

namespace PitWall.Jev.Requests;

/// <summary>Estrategia calculada de un piloto con la que se arma la consulta de estrategia a Jev.</summary>
/// <param name="State">Estado de carrera con el que se calculó <paramref name="Snapshot"/> (el hipotético en un escenario).</param>
/// <param name="Snapshot">Degradación, energía, reglas y Monte Carlo del piloto.</param>
/// <param name="Injected">Eventos inyectados vigentes que viajan en <c>injected</c> del state.</param>
public sealed record JevStrategySource(
    RaceState State,
    StrategySnapshot Snapshot,
    IReadOnlyList<string> Injected)
{
    /// <summary>
    /// Fuente de la consulta: sin escenario hipotético, la real. Con uno, su estrategia (estado y Monte Carlo con los
    /// eventos aplicados); si el escenario no tiene eventos vigentes la hipotética es igual a la real y se usa esa.
    /// Si tiene eventos vigentes pero su estrategia todavía no se calculó, null: la real nunca se consulta como
    /// hipotética.
    /// </summary>
    /// <param name="hypotheticalScenario">Hay un escenario distinto de <c>Real</c> activo.</param>
    /// <param name="scenarioHasActiveEvents">El escenario tiene eventos con efecto en la vuelta actual.</param>
    public static JevStrategySource? Select(
        JevStrategySource? real,
        JevStrategySource? hypothetical,
        bool hypotheticalScenario,
        bool scenarioHasActiveEvents)
    {
        if (!hypotheticalScenario)
        {
            return real;
        }

        return hypothetical ?? (scenarioHasActiveEvents ? null : real);
    }
}
