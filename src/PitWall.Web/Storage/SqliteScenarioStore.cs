using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using PitWall.Data;
using PitWall.Strategy.Scenarios;

using InjectedEventEntity = PitWall.Data.Entities.InjectedEvent;
using ScenarioEntity = PitWall.Data.Entities.Scenario;

namespace PitWall.Web.Storage;

/// <summary>
/// Escenarios en las tablas <c>Scenario</c> e <c>InjectedEvent</c>; los parámetros viajan como JSON en <c>Params</c>.
/// Los ids los asigna este store (último id de cada tabla + 1, leído una vez) en lugar de SQLite, para poder
/// reservarlos sin esperar a la escritura. Supone que esta app es la única que escribe esas tablas.
/// </summary>
public sealed class SqliteScenarioStore(IDbContextFactory<PitWallDbContext> dbContextFactory, TimeProvider timeProvider) : IScenarioStore
{
    private readonly SemaphoreSlim _idsLoading = new(1, 1);
    private bool _idsLoaded;
    private int _lastScenarioId;
    private int _lastEventId;

    public async Task<IReadOnlyList<Scenario>> LoadAsync(int sessionKey, CancellationToken ct)
    {
        await EnsureIdsAsync(ct);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var scenarios = await db.Scenarios.Where(e => e.SessionKey == sessionKey).OrderBy(e => e.Id).ToListAsync(ct);
        if (!scenarios.Any(e => e.Name == Scenario.RealName))
        {
            var real = ToEntity(await NewScenarioAsync(sessionKey, Scenario.RealName, ct));
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

    public async Task<Scenario> NewScenarioAsync(int sessionKey, string name, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        await EnsureIdsAsync(ct);
        return new Scenario(ScenarioKeys.ToGuid(Interlocked.Increment(ref _lastScenarioId)), sessionKey, name, timeProvider.GetUtcNow(), []);
    }

    public async Task<InjectedEvent> NewEventAsync(
        Guid scenarioId,
        InjectedEventType type,
        int lap,
        InjectedEventParameters parameters,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        _ = DatabaseId(scenarioId);

        await EnsureIdsAsync(ct);
        return new InjectedEvent(Interlocked.Increment(ref _lastEventId), scenarioId, type, lap, parameters, timeProvider.GetUtcNow());
    }

    public async Task SaveAsync(Scenario? scenario, InjectedEvent? injected, CancellationToken ct)
    {
        if (scenario is null && injected is null)
        {
            return;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        if (scenario is not null)
        {
            db.Scenarios.Add(ToEntity(scenario));
        }

        if (injected is not null)
        {
            db.InjectedEvents.Add(new InjectedEventEntity
            {
                Id = injected.Id,
                ScenarioId = DatabaseId(injected.ScenarioId),
                Lap = injected.Lap,
                Type = injected.Type.ToString(),
                Params = JsonSerializer.Serialize(injected.Parameters, ScenarioJsonContext.Default.InjectedEventParameters),
                CreatedAt = injected.CreatedAt,
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureIdsAsync(CancellationToken ct)
    {
        if (Volatile.Read(ref _idsLoaded))
        {
            return;
        }

        await _idsLoading.WaitAsync(ct);
        try
        {
            if (_idsLoaded)
            {
                return;
            }

            await using var db = await dbContextFactory.CreateDbContextAsync(ct);
            _lastScenarioId = await db.Scenarios.MaxAsync(e => (int?)e.Id, ct) ?? 0;
            _lastEventId = await db.InjectedEvents.MaxAsync(e => (int?)e.Id, ct) ?? 0;
            Volatile.Write(ref _idsLoaded, true);
        }
        finally
        {
            _idsLoading.Release();
        }
    }

    private static int DatabaseId(Guid scenarioId) =>
        ScenarioKeys.ToDatabaseId(scenarioId) ?? throw new ArgumentException($"Scenario {scenarioId} does not come from this store.", nameof(scenarioId));

    private static ScenarioEntity ToEntity(Scenario scenario) => new()
    {
        Id = DatabaseId(scenario.Id),
        SessionKey = scenario.SessionKey,
        Name = scenario.Name,
        CreatedAt = scenario.CreatedAt,
    };

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
