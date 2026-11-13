namespace PitWall.Strategy.Scenarios;

/// <summary>
/// Persistencia de escenarios e inyecciones (tablas <c>Scenario</c> e <c>InjectedEvent</c>). Los ids se reservan
/// antes de escribir: quien inyecta puede calcular y publicar con el id definitivo mientras la escritura corre.
/// </summary>
public interface IScenarioStore
{
    /// <summary>Escenarios de la sesión con sus eventos, en orden de creación; crea <see cref="Scenario.RealName"/> si falta.</summary>
    Task<IReadOnlyList<Scenario>> LoadAsync(int sessionKey, CancellationToken ct);

    /// <summary>Escenario vacío con id reservado; no existe en la base hasta <see cref="SaveAsync"/>.</summary>
    Task<Scenario> NewScenarioAsync(int sessionKey, string name, CancellationToken ct);

    /// <summary>Evento con id reservado; no existe en la base hasta <see cref="SaveAsync"/>.</summary>
    Task<InjectedEvent> NewEventAsync(Guid scenarioId, InjectedEventType type, int lap, InjectedEventParameters parameters, CancellationToken ct);

    /// <summary>Escribe en una sola transacción el escenario nuevo (si hay) y el evento reservados.</summary>
    /// <param name="scenario">Escenario de <see cref="NewScenarioAsync"/> que todavía no se escribió, o null.</param>
    /// <param name="injected">Evento de <see cref="NewEventAsync"/>, o null.</param>
    Task SaveAsync(Scenario? scenario, InjectedEvent? injected, CancellationToken ct);
}
