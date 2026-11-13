using System.Text.Json.Serialization;

using PitWall.Strategy.Scenarios;

namespace PitWall.Web.Services;

/// <summary>Escenarios de la sesión cargada y el activo; lo que muestran el panel de inyección y el hub.</summary>
/// <param name="Error">Último error al leer, crear o inyectar (p. ej. SQLite no disponible), o null.</param>
public sealed record ScenarioView(
    int? SessionKey,
    IReadOnlyList<Scenario> Scenarios,
    Guid? ActiveScenarioId,
    string? Error)
{
    public static ScenarioView Empty { get; } = new(null, [], null, null);

    [JsonIgnore]
    public Scenario? Active => Scenarios.FirstOrDefault(scenario => scenario.Id == ActiveScenarioId);

    /// <summary>El escenario activo no es <c>Real</c>: todo lo que produce es HIPOTÉTICO.</summary>
    public bool Hypothetical => Active is { IsReal: false };
}
