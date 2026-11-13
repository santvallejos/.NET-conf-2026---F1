using PitWall.Domain;

namespace PitWall.Jev.Decisions;

/// <summary>Datos de la decisión que no vienen de Jev.</summary>
/// <param name="ScenarioId">null = carrera real; distinto de null = hipotético.</param>
/// <param name="Reasons">Generadas por el motor de reglas (ADR-009).</param>
public sealed record JevDecisionInput(
    Guid Id,
    int SessionKey,
    Guid? ScenarioId,
    int DriverNumber,
    int Lap,
    JevTrigger Trigger,
    IReadOnlyList<Reason> Reasons);
