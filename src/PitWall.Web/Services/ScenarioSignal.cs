using PitWall.Strategy.Scenarios;

namespace PitWall.Web.Services;

/// <summary>Cambio en los escenarios que obliga a recalcular la estrategia hipotética.</summary>
/// <param name="Injected">Evento recién inyectado con efecto inmediato (<c>lap</c> ≤ vuelta actual), o null.</param>
internal sealed record ScenarioSignal(InjectedEvent? Injected);
