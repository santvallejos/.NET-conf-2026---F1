namespace PitWall.Strategy.Scenarios;

/// <summary>Persistencia de escenarios e inyecciones (tablas <c>Scenario</c> e <c>InjectedEvent</c>).</summary>
public interface IScenarioStore
{
    /// <summary>Escenarios de la sesión con sus eventos, en orden de creación; crea <see cref="Scenario.RealName"/> si falta.</summary>
    Task<IReadOnlyList<Scenario>> LoadAsync(int sessionKey, CancellationToken ct);

    Task<Scenario> CreateAsync(int sessionKey, string name, CancellationToken ct);

    Task<InjectedEvent> AddEventAsync(Guid scenarioId, InjectedEventType type, int lap, InjectedEventParameters parameters, CancellationToken ct);
}
