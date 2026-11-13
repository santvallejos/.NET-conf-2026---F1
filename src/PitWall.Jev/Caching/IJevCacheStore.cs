using PitWall.Domain;

namespace PitWall.Jev.Caching;

/// <summary>
/// Caché de respuestas de Jev por hash del request normalizado. Sin EF Core: la implementación persistente
/// vive en el host (docs/architecture.md §2).
/// </summary>
public interface IJevCacheStore
{
    /// <returns>JSON de las respuestas, o null si no hay entrada.</returns>
    Task<string?> GetAsync(string stateHash, CancellationToken ct);

    Task SetAsync(string stateHash, JevKind kind, string responseJson, CancellationToken ct);
}
