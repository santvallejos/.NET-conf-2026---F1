using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using PitWall.Data;
using PitWall.Strategy.Scenarios;

using InjectedEventEntity = PitWall.Data.Entities.InjectedEvent;
using ScenarioEntity = PitWall.Data.Entities.Scenario;

namespace PitWall.Web.Storage;

/// <summary>Escenarios en las tablas <c>Scenario</c> e <c>InjectedEvent</c>; los parámetros viajan como JSON en <c>Params</c>.</summary>
public sealed class SqliteScenarioStore(IDbContextFactory<PitWallDbContext> dbContextFactory, TimeProvider timeProvider) : IScenarioStore
{
    public async Task<IReadOnlyList<Scenario>> LoadAsync(int sessionKey, CancellationToken ct)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var scenarios = await db.Scenarios.Where(e => e.SessionKey == sessionKey).OrderBy(e => e.Id).ToListAsync(ct);
        if (!scenarios.Any(e => e.Name == Scenario.RealName))
        {
            var real = new ScenarioEntity { SessionKey = sessionKey, Name = Scenario.RealName, CreatedAt = timeProvider.GetUtcNow() };
            db.Scenarios.Add(real);
            await db.SaveChangesAsync(ct);
            scenarios.Insert(0, real);
        }

        var ids = scenarios.Select(e => e.Id).ToList();
        var events = await db.InjectedEvents.AsNoTracking()
            .Where(e => ids.Contains(e.ScenarioId))
            .OrderBy(e => e.Id)
            .ToListAsync(ct);
        var byScenario = events.ToLookup(e => e.ScenarioId);

        return [.. scenarios.Select(e => Map(e, [.. byScenario[e.Id].Select(Map).OfType<InjectedEvent>()]))];
    }

    public async Task<Scenario> CreateAsync(int sessionKey, string name, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var entity = new ScenarioEntity { SessionKey = sessionKey, Name = name, CreatedAt = timeProvider.GetUtcNow() };
        db.Scenarios.Add(entity);
        await db.SaveChangesAsync(ct);
        return Map(entity, []);
    }

    public async Task<InjectedEvent> AddEventAsync(
        Guid scenarioId,
        InjectedEventType type,
        int lap,
        InjectedEventParameters parameters,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var id = ScenarioKeys.ToDatabaseId(scenarioId)
            ?? throw new ArgumentException($"Scenario {scenarioId} is not persisted.", nameof(scenarioId));

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var entity = new InjectedEventEntity
        {
            ScenarioId = id,
            Lap = lap,
            Type = type.ToString(),
            Params = JsonSerializer.Serialize(parameters, ScenarioJsonContext.Default.InjectedEventParameters),
            CreatedAt = timeProvider.GetUtcNow(),
        };
        db.InjectedEvents.Add(entity);
        await db.SaveChangesAsync(ct);
        return new InjectedEvent(entity.Id, scenarioId, type, lap, parameters, entity.CreatedAt);
    }

    private static Scenario Map(ScenarioEntity entity, IReadOnlyList<InjectedEvent> events) =>
        new(ScenarioKeys.ToGuid(entity.Id), entity.SessionKey, entity.Name, entity.CreatedAt, events);

    // Un tipo desconocido (fila escrita por otra versión) se ignora.
    private static InjectedEvent? Map(InjectedEventEntity entity) =>
        Enum.TryParse<InjectedEventType>(entity.Type, out var type)
            ? new InjectedEvent(
                entity.Id,
                ScenarioKeys.ToGuid(entity.ScenarioId),
                type,
                entity.Lap,
                JsonSerializer.Deserialize(entity.Params, ScenarioJsonContext.Default.InjectedEventParameters) ?? new InjectedEventParameters(),
                entity.CreatedAt)
            : null;
}
