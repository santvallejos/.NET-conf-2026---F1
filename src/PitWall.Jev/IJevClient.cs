namespace PitWall.Jev;

/// <summary>Cliente de Jev System One (docs/jev.md §5).</summary>
public interface IJevClient
{
    Task<JevResponse> SystemOneAsync(JevRequest request, CancellationToken ct);
}
