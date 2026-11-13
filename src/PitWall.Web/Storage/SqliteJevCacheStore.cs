using System.Data.Common;

using Microsoft.EntityFrameworkCore;

using PitWall.Data;
using PitWall.Data.Entities;
using PitWall.Domain;
using PitWall.Jev.Caching;

namespace PitWall.Web.Storage;

/// <summary>
/// Caché de Jev en la tabla <c>JevCache</c>. Un fallo de la base se registra y se trata como caché vacía:
/// Jev sigue funcionando, solo que sin ahorro.
/// </summary>
public sealed partial class SqliteJevCacheStore(
    IDbContextFactory<PitWallDbContext> dbContextFactory,
    TimeProvider timeProvider,
    ILogger<SqliteJevCacheStore> logger) : IJevCacheStore
{
    public async Task<string?> GetAsync(string stateHash, CancellationToken ct)
    {
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(ct);
            return await db.JevCache
                .AsNoTracking()
                .Where(e => e.StateHash == stateHash)
                .Select(e => e.ResponseJson)
                .FirstOrDefaultAsync(ct);
        }
        catch (DbException ex)
        {
            LogCacheUnavailable(ex, stateHash);
            return null;
        }
    }

    public async Task SetAsync(string stateHash, JevKind kind, string responseJson, CancellationToken ct)
    {
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(ct);
            var entry = await db.JevCache.FirstOrDefaultAsync(e => e.StateHash == stateHash, ct);
            if (entry is null)
            {
                db.JevCache.Add(new JevCache
                {
                    StateHash = stateHash,
                    Kind = kind,
                    ResponseJson = responseJson,
                    CreatedAt = timeProvider.GetUtcNow(),
                });
            }
            else
            {
                entry.Kind = kind;
                entry.ResponseJson = responseJson;
            }

            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            LogCacheUnavailable(ex, stateHash);
        }
        catch (DbException ex)
        {
            LogCacheUnavailable(ex, stateHash);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Caché de Jev no disponible para {StateHash}; se continúa sin caché.")]
    private partial void LogCacheUnavailable(Exception exception, string stateHash);
}
