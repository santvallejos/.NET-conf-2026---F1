using PitWall.Strategy.Scenarios;

namespace PitWall.Web.Services;

/// <summary>Cambio en los escenarios que obliga a recalcular la estrategia hipotética.</summary>
/// <param name="Injected">Evento recién inyectado con efecto inmediato (<c>lap</c> ≤ vuelta actual), o null.</param>
/// <param name="Saved">
/// Escritura en SQLite del evento recién inyectado, que corre en paralelo con el cálculo: true si quedó guardado.
/// El triage de Jev la espera (su decisión referencia el escenario en la base); el cálculo y la publicación no.
/// </param>
internal sealed record ScenarioSignal(InjectedEvent? Injected, Task<bool> Saved)
{
    /// <summary>Recalcular sin evento nuevo (escenario creado, elegido o cargado).</summary>
    public static ScenarioSignal Recalculate { get; } = new(null, Task.FromResult(true));
}
