using System.Data.Common;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using PitWall.Data;
using PitWall.Data.Entities;
using PitWall.Jev.Decisions;
using PitWall.Web.Hubs;

namespace PitWall.Web.Storage;

/// <summary>
/// Historial de decisiones en la tabla <c>JevDecisionLog</c>. <c>ResponseJson</c> guarda la <c>JevDecision</c>
/// completa con el mismo JSON que publica el hub (incluye trigger, reasons y probabilidades). Un fallo de la base se
/// registra y la decisión sigue publicándose.
/// </summary>
public sealed partial class SqliteJevDecisionLog(
    IDbContextFactory<PitWallDbContext> dbContextFactory,
    TimeProvider timeProvider,
    ILogger<SqliteJevDecisionLog> logger) : IJevDecisionLog
{
    public async Task AppendAsync(JevDecisionLogEntry entry, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var decision = entry.Decision;
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(ct);
            db.JevDecisionLog.Add(new JevDecisionLog
            {
                SessionKey = decision.SessionKey,

                // La columna referencia a Scenario (id entero) y la decisión usa Guid: hasta que los escenarios se
                // persistan queda null; ScenarioId e Hypothetical viajan igual en ResponseJson.
                ScenarioId = null,
                DriverNumber = decision.DriverNumber,
                Lap = decision.Lap,
                Kind = decision.Kind,
                StateHash = entry.StateHash,
                RequestJson = entry.RequestJson,
                ResponseJson = JsonSerializer.Serialize(decision, PitWallHubJsonContext.Default.JevDecision),
                LatencyMs = decision.LatencyMs,
                Tokens = decision.Tokens,
                CostUsd = (double)decision.CostUsd,
                CreatedAt = timeProvider.GetUtcNow(),
            });
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            LogLogUnavailable(ex, decision.DriverNumber, decision.Lap);
        }
        catch (DbException ex)
        {
            LogLogUnavailable(ex, decision.DriverNumber, decision.Lap);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudo guardar la decisión de Jev del piloto {DriverNumber} en la vuelta {Lap}; se continúa sin historial.")]
    private partial void LogLogUnavailable(Exception exception, int driverNumber, int lap);
}
