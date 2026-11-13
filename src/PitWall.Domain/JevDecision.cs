namespace PitWall.Domain;

/// <summary>Decisión final de Jev; contrato público sin texto libre (docs/jev.md §4).</summary>
/// <param name="ScenarioId">null = carrera real; != null = hipotético.</param>
/// <param name="Confidence">Mínimo de las confidences de las preguntas.</param>
/// <param name="Reasons">Generadas por el motor de reglas, no por Jev.</param>
public sealed record JevDecision(
    Guid Id,
    int SessionKey,
    Guid? ScenarioId,
    int DriverNumber,
    int Lap,
    JevKind Kind,
    JevTrigger Trigger,
    PitCall? PitCall,
    Compound? Compound,
    PaceMode? PaceMode,
    EnergyMode? EnergyMode,
    Impact? Impact,
    int? Urgency,
    double Confidence,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> Probabilities,
    IReadOnlyList<Reason> Reasons,
    bool Hypothetical,
    bool FromCache,
    int LatencyMs,
    int Tokens,
    decimal CostUsd);
