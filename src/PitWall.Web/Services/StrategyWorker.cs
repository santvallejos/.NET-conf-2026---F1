using System.Data.Common;
using System.Threading.Channels;

using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using PitWall.Data;
using PitWall.Domain;
using PitWall.Replay;
using PitWall.Strategy.Scenarios;
using PitWall.Strategy.Workers;
using PitWall.Web.Components.Panels.Energy;
using PitWall.Web.Hubs;

namespace PitWall.Web.Services;

/// <summary>
/// Calcula en segundo plano la estrategia de los 4 pilotos (foco + <c>PitWall:BackgroundDrivers</c>) cada vez que
/// cierran una vuelta en el replay (docs/architecture.md §3): degradación, energía, reglas y Monte Carlo. Publica
/// el resultado en <see cref="StrategyStore"/> y por <see cref="PitWallHub"/>. Si llegan varios estados mientras
/// calcula, solo procesa el último.
/// Con un escenario hipotético activo calcula además, sobre el estado hipotético, la estrategia con sus eventos
/// inyectados (<see cref="ScenarioStrategyStore"/>) y dispara el triage de Jev de cada evento cuando ocurre: al
/// inyectarlo si su vuelta ya pasó, o cuando el replay llega a ella. El replay y la estrategia real no cambian.
/// </summary>
public sealed partial class StrategyWorker(
    ReplayEngine engine,
    PitWallSession session,
    IDbContextFactory<PitWallDbContext> dbContextFactory,
    EnergyEstimateService energy,
    StrategyCalculator calculator,
    StrategyStore store,
    ScenarioService scenarios,
    ScenarioStrategyStore hypotheticalStore,
    JevTriggers jevTriggers,
    StrategyMetrics metrics,
    IHubContext<PitWallHub, IPitWallClient> hub,
    IOptions<PitWallOptions> options,
    IOptions<ScenarioOptions> scenarioOptions,
    ILogger<StrategyWorker> logger)
    : BackgroundService
{
    private readonly int[] _drivers = [.. new[] { options.Value.FocusDriver }.Concat(options.Value.BackgroundDrivers).Distinct()];

    // El replay y los cambios de escenario calculan de a uno: comparten la sesión cargada y lo ya calculado.
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<int, int> _lastLaps = [];
    private readonly Dictionary<int, int> _lastHypotheticalLaps = [];
    private HashSet<int> _activatedEvents = [];
    private Guid? _scenarioId;
    private string? _effectsKey;
    private StrategySessionData? _data;
    private RaceState? _latest;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var subscription = engine.Subscribe();
        try
        {
            await Task.WhenAll(WatchReplayAsync(subscription.Reader, stoppingToken), WatchScenariosAsync(stoppingToken));
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

            await _gate.WaitAsync(cancellationToken);
            try
            {
                _latest = state;
                if (_data?.SessionKey != state.SessionKey)
                {
                    _data = await LoadAsync(state.SessionKey, cancellationToken);
                    _lastLaps.Clear();
                    _lastHypotheticalLaps.Clear();
                    _scenarioId = null;
                    _effectsKey = null;
                    store.Reset(state.SessionKey);
                    await scenarios.EnsureLoadedAsync(state.SessionKey, cancellationToken);
                }

                await CalculateAsync(state, _data, cancellationToken);
                await CalculateScenarioAsync(state, _data, null, force: false, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogCalculationFailed(exception, state.Clock);
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    // Escenario creado, elegido o con un evento nuevo: recalcula la estrategia hipotética con el último estado.
    private async Task WatchScenariosAsync(CancellationToken cancellationToken)
    {
        await foreach (var signal in scenarios.Signals.ReadAllAsync(cancellationToken))
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                if (_latest is { } state && _data is { } data && data.SessionKey == state.SessionKey)
                {
                    await CalculateScenarioAsync(state, data, signal.Injected, force: true, cancellationToken);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogScenarioFailed(exception);
            }
            finally
            {
                _gate.Release();
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
        foreach (var car in due)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _lastLaps[car.DriverNumber] = car.Lap;
            if (context is null || calculator.Calculate(context, car.DriverNumber) is not { } result)
            {
                continue;
            }

            store.Publish(state, result);
            metrics.RecordMonteCarlo(result.MonteCarloDuration, car.DriverNumber);
            LogCalculated(car.DriverNumber, result.Snapshot.Lap, result.Snapshot.Outcomes.Count, result.Iterations, result.Duration.TotalMilliseconds);
            await hub.Clients.All.OnStrategySnapshot(result.Snapshot);
        }
    }

    /// <summary>
    /// Estrategia hipotética del escenario activo y triage de los eventos que ocurren ahora. Los eventos de un escenario
    /// recién elegido que ya habían ocurrido no disparan triage; al saltar hacia atrás en el replay, sí al volver a ocurrir.
    /// </summary>
    /// <param name="injected">Evento recién inyectado con efecto inmediato.</param>
    /// <param name="force">Recalcular aunque ningún piloto haya cambiado de vuelta.</param>
    private async Task CalculateScenarioAsync(RaceState state, StrategySessionData data, InjectedEvent? injected, bool force, CancellationToken cancellationToken)
    {
        var view = scenarios.View;
        if (view.SessionKey != state.SessionKey || view.Active is not { IsReal: false } scenario)
        {
            _scenarioId = null;
            _lastHypotheticalLaps.Clear();
            if (_effectsKey is not null || force)
            {
                _effectsKey = null;
                hypotheticalStore.SetEffects(state.SessionKey, null, ScenarioEffects.None);
            }

            return;
        }

        var raceLap = state.LeaderLap;
        if (_scenarioId != scenario.Id)
        {
            _scenarioId = scenario.Id;
            _lastHypotheticalLaps.Clear();
            _activatedEvents = [.. scenario.Events.Where(e => e.Lap <= raceLap && e.Id != injected?.Id).Select(e => e.Id)];
            force = true;
        }

        _activatedEvents.RemoveWhere(id => scenario.Events.FirstOrDefault(e => e.Id == id) is not { } known || known.Lap > raceLap);
        var occurring = scenario.Events.Where(e => e.Lap <= raceLap && !_activatedEvents.Contains(e.Id)).ToList();
        var effects = ScenarioEffects.Resolve(scenario.Id, scenario.Events, raceLap, data.StintStartLaps(state), scenarioOptions.Value);
        // Se resuelve en cada estado del replay, pero el store (y los paneles) solo se entera si algo cambió.
        var effectsKey = $"{scenario.Id}|{raceLap}|{scenario.Events.Count}|{string.Join(',', effects.ActiveEvents.Select(e => e.Id))}";
        if (force || effectsKey != _effectsKey)
        {
            _effectsKey = effectsKey;
            hypotheticalStore.SetEffects(state.SessionKey, scenario.Id, effects);
        }
        if (effects.ActiveEvents.Count == 0)
        {
            // Sin eventos vigentes la estrategia hipotética es la real: nada que calcular ni que consultar.
            _lastHypotheticalLaps.Clear();
            _activatedEvents.UnionWith(occurring.Select(e => e.Id));
            return;
        }

        force |= occurring.Count > 0;
        var due = state.Cars
            .Where(car => _drivers.Contains(car.DriverNumber)
                && (force || !_lastHypotheticalLaps.TryGetValue(car.DriverNumber, out var lap) || lap != car.Lap))
            .ToList();
        if (due.Count > 0)
        {
            var hypothetical = effects.Apply(state);
            var context = data.Context(hypothetical, await EstimatesAsync(state, due, cancellationToken), effects);
            IReadOnlyList<string> active = [.. effects.ActiveEvents.Select(e => e.Describe())];
            foreach (var car in due)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _lastHypotheticalLaps[car.DriverNumber] = car.Lap;
                if (context is null || calculator.Calculate(context, car.DriverNumber) is not { } result)
                {
                    continue;
                }

                hypotheticalStore.Publish(new ScenarioPublication(state.SessionKey, scenario.Id, new StrategyPublication(hypothetical, result), active));
                metrics.RecordMonteCarlo(result.MonteCarloDuration, car.DriverNumber);
                await hub.Clients.All.OnHypotheticalStrategySnapshot(scenario.Id, result.Snapshot);
            }
        }

        // Triage después de publicar: Jev ve la estrategia que ya incluye el evento.
        foreach (var occurred in occurring.Where(e => effects.ActiveEvents.Contains(e)))
        {
            jevTriggers.RequestTriage(occurred.Describe(), scenario.Id, occurred.Id);
        }

        _activatedEvents.UnionWith(occurring.Select(e => e.Id));
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

    [LoggerMessage(Level = LogLevel.Error, Message = "Hypothetical strategy calculation failed")]
    private partial void LogScenarioFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read session {SessionKey} for the strategy worker")]
    private partial void LogSessionUnavailable(Exception exception, int sessionKey);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Strategy for driver {DriverNumber} on lap {Lap}: {Candidates} candidates × {Iterations} iterations in {Elapsed} ms")]
    private partial void LogCalculated(int driverNumber, int lap, int candidates, int iterations, double elapsed);
}
