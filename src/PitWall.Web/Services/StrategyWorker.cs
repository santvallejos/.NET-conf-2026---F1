using System.Data.Common;

using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using PitWall.Data;
using PitWall.Domain;
using PitWall.Replay;
using PitWall.Strategy.Workers;
using PitWall.Web.Components.Panels.Energy;
using PitWall.Web.Hubs;

namespace PitWall.Web.Services;

/// <summary>
/// Calcula en segundo plano la estrategia de los 4 pilotos (foco + <c>PitWall:BackgroundDrivers</c>) cada vez que
/// cierran una vuelta en el replay (docs/architecture.md §3): degradación, energía, reglas y Monte Carlo. Publica
/// el resultado en <see cref="StrategyStore"/> y por <see cref="PitWallHub"/>. Si llegan varios estados mientras
/// calcula, solo procesa el último.
/// </summary>
public sealed class StrategyWorker(
    ReplayEngine engine,
    PitWallSession session,
    IDbContextFactory<PitWallDbContext> dbContextFactory,
    EnergyEstimateService energy,
    StrategyCalculator calculator,
    StrategyStore store,
    StrategyMetrics metrics,
    IHubContext<PitWallHub, IPitWallClient> hub,
    IOptions<PitWallOptions> options,
    ILogger<StrategyWorker> logger)
    : BackgroundService
{
    private readonly int[] _drivers = [.. new[] { options.Value.FocusDriver }.Concat(options.Value.BackgroundDrivers).Distinct()];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var subscription = engine.Subscribe();
        StrategySessionData? data = null;
        var lastLaps = new Dictionary<int, int>();
        try
        {
            while (await subscription.Reader.WaitToReadAsync(stoppingToken))
            {
                RaceState? state = null;
                while (subscription.Reader.TryRead(out var next))
                {
                    state = next;
                }

                if (state is null)
                {
                    continue;
                }

                try
                {
                    if (data?.SessionKey != state.SessionKey)
                    {
                        data = await LoadAsync(state.SessionKey, stoppingToken);
                        lastLaps.Clear();
                        store.Reset(state.SessionKey);
                    }

                    await CalculateAsync(state, data, lastLaps, stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception, "Strategy calculation at {Clock} failed", state.Clock);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task<StrategySessionData> LoadAsync(int sessionKey, CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await StrategySessionData.LoadAsync(db, sessionKey, cancellationToken);
        }
        catch (DbException exception)
        {
            logger.LogWarning(exception, "Could not read session {SessionKey} for the strategy worker", sessionKey);
            return StrategySessionData.Empty(sessionKey);
        }
    }

    // Una vuelta "cierra" cuando cambia la vuelta en curso del piloto (también al saltar en el replay).
    private async Task CalculateAsync(RaceState state, StrategySessionData data, Dictionary<int, int> lastLaps, CancellationToken cancellationToken)
    {
        var due = state.Cars
            .Where(car => _drivers.Contains(car.DriverNumber) && (!lastLaps.TryGetValue(car.DriverNumber, out var lap) || lap != car.Lap))
            .ToList();
        if (due.Count == 0)
        {
            return;
        }

        if (!energy.IsLoaded(state.SessionKey))
        {
            await energy.LoadAsync(state.SessionKey, cancellationToken);
        }

        var view = session.View with { State = state };
        var estimates = new Dictionary<int, EnergyEstimate>();
        foreach (var car in due)
        {
            if (energy.Get(view with { FocusDriver = car.DriverNumber })?.Estimate is { } estimate)
            {
                estimates[car.DriverNumber] = estimate;
            }
        }

        var context = data.Context(state, estimates);
        foreach (var car in due)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lastLaps[car.DriverNumber] = car.Lap;
            if (context is null || calculator.Calculate(context, car.DriverNumber) is not { } result)
            {
                continue;
            }

            store.Publish(state, result);
            metrics.RecordMonteCarlo(result.MonteCarloDuration, car.DriverNumber);
            logger.LogDebug(
                "Strategy for driver {DriverNumber} on lap {Lap}: {Candidates} candidates × {Iterations} iterations in {Elapsed} ms",
                car.DriverNumber,
                result.Snapshot.Lap,
                result.Snapshot.Outcomes.Count,
                result.Iterations,
                result.Duration.TotalMilliseconds);
            await hub.Clients.All.OnStrategySnapshot(result.Snapshot);
        }
    }
}
