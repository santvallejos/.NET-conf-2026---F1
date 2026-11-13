namespace PitWall.Jev.Decisions;

/// <summary>
/// Historial persistente de decisiones de Jev (<c>JevDecisionLog</c>). Sin EF Core: la implementación vive en el
/// host (docs/architecture.md §2).
/// </summary>
public interface IJevDecisionLog
{
    Task AppendAsync(JevDecisionLogEntry entry, CancellationToken ct);
}
