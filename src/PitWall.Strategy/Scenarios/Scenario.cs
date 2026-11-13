namespace PitWall.Strategy.Scenarios;

/// <summary>
/// Inyecciones hipotéticas sobre una sesión (docs/scenarios.md §2). <see cref="RealName"/> es el escenario sin
/// inyecciones, que siempre existe; todo lo producido en otro escenario es hipotético.
/// </summary>
public sealed record Scenario(
    Guid Id,
    int SessionKey,
    string Name,
    DateTimeOffset CreatedAt,
    IReadOnlyList<InjectedEvent> Events)
{
    public const string RealName = "Real";

    public bool IsReal => string.Equals(Name, RealName, StringComparison.Ordinal);
}
