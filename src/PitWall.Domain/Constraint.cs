namespace PitWall.Domain;

/// <summary>
/// Restricción dura del motor de reglas (docs/strategy-engine.md §3.1): elimina candidatas antes de
/// Monte Carlo y bloquea decisiones de Jev que la violen.
/// </summary>
/// <param name="Rule">Identificador de la regla que la generó.</param>
/// <param name="ForbiddenCalls">Pit calls prohibidas (p. ej. <c>BOX</c> con bandera roja o pit lane cerrado).</param>
/// <param name="ForbiddenCompounds">Compuestos descartados (p. ej. secos con lluvia fuerte).</param>
/// <param name="ForcedCall">Pit call obligatoria (p. ej. <c>BOX</c> por pinchazo), o null.</param>
/// <param name="RequiresSecondDryCompound">Obligación de usar ≥ 2 compuestos secos distintos antes del final.</param>
public sealed record Constraint(
    string Rule,
    IReadOnlyList<PitCall> ForbiddenCalls,
    IReadOnlyList<Compound> ForbiddenCompounds,
    PitCall? ForcedCall,
    bool RequiresSecondDryCompound);
