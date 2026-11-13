using System.Collections.Concurrent;

using PitWall.Domain;

namespace PitWall.Jev.Caching;

/// <summary>Caché en memoria del proceso; se usa cuando el host no registra una persistente.</summary>
public sealed class InMemoryJevCacheStore : IJevCacheStore
{
    private readonly ConcurrentDictionary<string, string> _entries = new(StringComparer.Ordinal);

    public Task<string?> GetAsync(string stateHash, CancellationToken ct) =>
        Task.FromResult(_entries.TryGetValue(stateHash, out var json) ? json : null);

    public Task SetAsync(string stateHash, JevKind kind, string responseJson, CancellationToken ct)
    {
        _entries[stateHash] = responseJson;
        return Task.CompletedTask;
    }
}
