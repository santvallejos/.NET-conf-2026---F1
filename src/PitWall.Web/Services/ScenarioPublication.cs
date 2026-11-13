namespace PitWall.Web.Services;

/// <summary>Estrategia hipotética de un piloto en un escenario, con los eventos activos que la produjeron.</summary>
/// <param name="Publication">Resultado calculado sobre el estado hipotético (no el del replay).</param>
/// <param name="Injected">Descripción de los eventos activos; viaja en <c>injected</c> del state de Jev.</param>
public sealed record ScenarioPublication(
    int SessionKey,
    Guid ScenarioId,
    StrategyPublication Publication,
    IReadOnlyList<string> Injected);
