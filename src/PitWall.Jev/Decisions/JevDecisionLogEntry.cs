using PitWall.Domain;

namespace PitWall.Jev.Decisions;

/// <summary>Decisión a persistir junto con el request que la produjo.</summary>
/// <param name="StateHash">Hash del request normalizado (el de la caché).</param>
/// <param name="RequestJson">Request normalizado enviado a Jev.</param>
public sealed record JevDecisionLogEntry(JevDecision Decision, string StateHash, string RequestJson);
