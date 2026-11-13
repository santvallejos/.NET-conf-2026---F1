using System.Data.Common;
using System.Threading.Channels;

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
/// cierran una vuelta en el replay (docs/architecture.md §3): degradación, energía y pit loss. Publica
/// el resultado en <see cref="StrategyStore"/> y por <see cref="PitWallHub"/>. Si llegan varios estados mientras
/// calcula, solo procesa el último. El piloto en foco se calcula primero.
/// </summary>
public sealed partial class StrategyWorker(
    ReplayEngine engine,
    PitWallSession session,
    IDbContextFactory<PitWallDbContext> dbContextFactory,
    EnergyEstimateService energy,
    StrategyCalculator calculator,
    StrategyStore store,
    IHubContext<PitWallHub, IPitWallClient> hub,
    IOptions<PitWallOptions> options,
    ILogger<StrategyWorker> logger)
    : BackgroundService
{
    private readonly int[] _drivers = [.. new[] { options.Value.FocusDriver }.Concat(options.Value.BackgroundDrivers).Distinct()];
    private readonly Dictionary<int, int> _lastLaps = [];
    private StrategySessionData? _data;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var subscription = engine.Subscribe();
        try
        {
            await WatchReplayAsync(subscription.Reader, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task WatchReplayAsync(ChannelReader<RaceState> reader, CancellationToken cancellationToken)
    {
        while (await reader.WaitToReadAsync(cancellationToken))
        {
            RaceState? state = null;
            while (reader.TryRead(out var next))
            {
                state = next;
            }

            if (state is null)
            {
                continue;
            }

            try
            {
                if (_data?.SessionKey != state.SessionKey)
                {
                    _data = await LoadAsync(state.SessionKey, cancellationToken);
                    _lastLaps.Clear();
                    store.Reset(state.SessionKey);
                }

                await CalculateAsync(state, _data, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogCalculationFailed(exception, state.Clock);
            }
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
            LogSessionUnavailable(exception, sessionKey);
            return StrategySessionData.Empty(sessionKey);
        }
    }

    // Una vuelta "cierra" cuando cambia la vuelta en curso del piloto (también al saltar en el replay).
    private async Task CalculateAsync(RaceState state, StrategySessionData data, CancellationToken cancellationToken)
    {
        var due = state.Cars
            .Where(car => _drivers.Contains(car.DriverNumber) && (!_lastLaps.TryGetValue(car.DriverNumber, out var lap) || lap != car.Lap))
            .ToList();
        if (due.Count == 0)
        {
            return;
        }

        var context = data.Context(state, await EstimatesAsync(state, due, cancellationToken));
        foreach (var car in FocusFirst(due))
        {
            cancellationToken.ThrowIfCancellationRequested();
            _lastLaps[car.DriverNumber] = car.Lap;
            if (context is null || calculator.Calculate(context, car.DriverNumber) is not { } result)
            {
                continue;
            }

            store.Publish(state, result);
            LogCalculated(car.DriverNumber, result.Snapshot.Lap, result.Duration.TotalMilliseconds);
            await hub.Clients.All.OnStrategySnapshot(result.Snapshot);
        }
    }

    // El piloto en foco primero (es el que mira la UI); el resto en el orden de PitWall:BackgroundDrivers.
    private IEnumerable<CarState> FocusFirst(List<CarState> cars)
    {
        var focus = session.View.FocusDriver;
        return cars.OrderBy(car => car.DriverNumber == focus ? -1 : Array.IndexOf(_drivers, car.DriverNumber));
    }

    private async Task<Dictionary<int, EnergyEstimate>> EstimatesAsync(RaceState state, List<CarState> cars, CancellationToken cancellationToken)
    {
        if (!energy.IsLoaded(state.SessionKey))
        {
            await energy.LoadAsync(state.SessionKey, cancellationToken);
        }

        var view = session.View with { State = state };
        var estimates = new Dictionary<int, EnergyEstimate>();
        foreach (var car in cars)
        {
            if (energy.Get(view with { FocusDriver = car.DriverNumber })?.Estimate is { } estimate)
            {
                estimates[car.DriverNumber] = estimate;
            }
        }

        return estimates;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Strategy calculation at {Clock} failed")]
    private partial void LogCalculationFailed(Exception exception, DateTimeOffset clock);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read session {SessionKey} for the strategy worker")]
    private partial void LogSessionUnavailable(Exception exception, int sessionKey);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Strategy for driver {DriverNumber} on lap {Lap} in {Elapsed} ms")]
    private partial void LogCalculated(int driverNumber, int lap, double elapsed);
}
