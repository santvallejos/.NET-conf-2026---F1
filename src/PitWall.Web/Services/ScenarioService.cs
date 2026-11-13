using System.Data.Common;
using System.Threading.Channels;

using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using PitWall.Strategy.Scenarios;
using PitWall.Web.Hubs;

namespace PitWall.Web.Services;

/// <summary>
/// Escenarios de la sesión cargada (docs/scenarios.md §2 y §4): <c>Real</c> siempre existe, se pueden crear varios y
/// uno está activo. Inyectar sobre <c>Real</c> crea un escenario nuevo. Cada cambio avisa a
/// <see cref="StrategyWorker"/>, que recalcula la estrategia hipotética y, si el evento ya ocurrió
/// (<c>lap</c> ≤ vuelta actual), dispara el triage de Jev; si no, queda programado hasta que el replay llegue.
/// Un evento inyectado se publica y se calcula con ids ya reservados mientras se escribe en SQLite; si la escritura
/// falla, se quita de la vista y se recalcula sin él.
/// </summary>
public sealed partial class ScenarioService(
    IScenarioStore store,
    PitWallSession session,
    IHubContext<PitWallHub, IPitWallClient> hub,
    IOptions<ScenarioOptions> options,
    IOptions<PitWallOptions> pitWallOptions,
    TimeProvider timeProvider,
    ILogger<ScenarioService> logger)
{
    private const string StoreError = "No se pudo leer o guardar escenarios en SQLite: solo está disponible la carrera real.";

    private readonly Lock _gate = new();
    private readonly SemaphoreSlim _loading = new(1, 1);
    private readonly Channel<ScenarioSignal> _signals = Channel.CreateUnbounded<ScenarioSignal>(new UnboundedChannelOptions
    {
        SingleReader = true,
    });

    private ScenarioView _view = ScenarioView.Empty;

    /// <summary>Se dispara (desde cualquier hilo) cada vez que cambia <see cref="View"/>.</summary>
    public event Action? Changed;

    public ScenarioView View
    {
        get
        {
            lock (_gate)
            {
                return _view;
            }
        }
    }

    internal ChannelReader<ScenarioSignal> Signals => _signals.Reader;

    /// <summary>Carga los escenarios de la sesión (crea <c>Real</c> si falta) y deja <c>Real</c> activo.</summary>
    public async Task EnsureLoadedAsync(int sessionKey, CancellationToken cancellationToken = default)
    {
        if (View.SessionKey == sessionKey)
        {
            return;
        }

        await _loading.WaitAsync(cancellationToken);
        try
        {
            if (View.SessionKey == sessionKey)
            {
                return;
            }

            IReadOnlyList<Scenario> scenarios;
            string? error = null;
            try
            {
                scenarios = await store.LoadAsync(sessionKey, cancellationToken);
            }
            catch (Exception exception) when (exception is DbException or DbUpdateException)
            {
                LogStoreFailed(exception, sessionKey);
                scenarios = [new Scenario(Guid.Empty, sessionKey, Scenario.RealName, timeProvider.GetUtcNow(), [])];
                error = StoreError;
            }

            var real = scenarios.First(scenario => scenario.IsReal);
            await SetViewAsync(new ScenarioView(sessionKey, scenarios, real.Id, error));
        }
        finally
        {
            _loading.Release();
        }
    }

    /// <summary>Crea un escenario vacío y lo deja activo; null si no hay sesión o falla la base.</summary>
    public async Task<Scenario?> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        var view = View;
        var trimmed = name?.Trim() ?? string.Empty;
        if (view.SessionKey is not { } sessionKey)
        {
            await SetErrorAsync("Cargá una sesión antes de crear escenarios.");
            return null;
        }

        if (trimmed.Length == 0 || string.Equals(trimmed, Scenario.RealName, StringComparison.OrdinalIgnoreCase))
        {
            await SetErrorAsync($"El nombre del escenario no puede estar vacío ni ser \"{Scenario.RealName}\".");
            return null;
        }

        try
        {
            var scenario = await store.NewScenarioAsync(sessionKey, trimmed, cancellationToken);
            await store.SaveAsync(scenario, null, cancellationToken);
            await UpdateAsync(current => current with { Scenarios = [.. current.Scenarios, scenario], ActiveScenarioId = scenario.Id, Error = null }, ScenarioSignal.Recalculate);
            return scenario;
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException)
        {
            LogStoreFailed(exception, sessionKey);
            await SetErrorAsync(StoreError);
            return null;
        }
    }

    public async Task SelectAsync(Guid scenarioId)
    {
        if (View.Scenarios.All(scenario => scenario.Id != scenarioId))
        {
            return;
        }

        await UpdateAsync(current => current with { ActiveScenarioId = scenarioId, Error = null }, ScenarioSignal.Recalculate);
    }

    /// <summary>
    /// Agrega el evento al escenario activo (o a uno nuevo si el activo es <c>Real</c>) con los defaults de
    /// <c>Scenarios:*</c>, lo publica para el cálculo y espera a que quede guardado. Devuelve null y deja el motivo
    /// en <see cref="ScenarioView.Error"/> si no se pudo.
    /// </summary>
    public async Task<InjectedEvent?> InjectAsync(InjectEventRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var view = View;
        var sessionView = session.View;
        if (view.SessionKey is not { } sessionKey || sessionView.State is not { } state || state.SessionKey != sessionKey)
        {
            await SetErrorAsync("Cargá una sesión antes de inyectar eventos.");
            return null;
        }

        var lapCount = sessionView.Playback.LapCount;
        if (!Enum.IsDefined(request.Type) || request.Lap < 1 || (lapCount > 0 && request.Lap > lapCount))
        {
            await SetErrorAsync($"La vuelta del evento debe estar entre 1 y {lapCount}.");
            return null;
        }

        var focusDriver = pitWallOptions.Value.FocusDriver;
        var parameters = request.Parameters ?? new InjectedEventParameters();
        if (parameters.DurationLaps is < 1)
        {
            await SetErrorAsync("La duración debe ser de al menos 1 vuelta.");
            return null;
        }

        if (request.Type == InjectedEventType.RivalCrash && parameters.Rival is null)
        {
            var reference = parameters.Driver ?? focusDriver;
            var relative = parameters.Relative ?? RivalRelative.Ahead;
            if (InjectedEventDefaults.RivalOf(state, reference, relative) is not { } rival)
            {
                await SetErrorAsync($"No hay un rival {(relative == RivalRelative.Ahead ? "adelante" : "atrás")} de #{reference}.");
                return null;
            }

            parameters = parameters with { Rival = rival, Relative = relative };
        }

        parameters = InjectedEventDefaults.Apply(request.Type, parameters, options.Value, focusDriver);
        var scenario = view.Active;
        Scenario? created = null;
        InjectedEvent injected;
        try
        {
            if (scenario is null || scenario.IsReal)
            {
                created = await store.NewScenarioAsync(sessionKey, ScenarioLabels.DefaultScenarioName(request.Type, request.Lap), cancellationToken);
                scenario = created;
            }

            injected = await store.NewEventAsync(scenario.Id, request.Type, request.Lap, parameters, cancellationToken);
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException)
        {
            LogStoreFailed(exception, sessionKey);
            await SetErrorAsync(StoreError);
            return null;
        }

        // Task.Run: Microsoft.Data.Sqlite escribe de forma sincrónica aunque la API sea async.
        var saved = Task.Run(() => SaveAsync(created, injected, sessionKey), CancellationToken.None);
        var immediate = request.Lap <= state.LeaderLap;
        await UpdateAsync(
            current =>
            {
                var target = current.Scenarios.FirstOrDefault(other => other.Id == injected.ScenarioId) ?? scenario;
                return current with
                {
                    Scenarios = [.. current.Scenarios.Where(other => other.Id != target.Id), target with { Events = [.. target.Events, injected] }],
                    ActiveScenarioId = target.Id,
                    Error = null,
                };
            },
            new ScenarioSignal(immediate ? injected : null, saved));
        LogInjected(injected.Describe(), scenario.Name, immediate);

        if (await saved)
        {
            return injected;
        }

        // Sin fila en la base el evento (y el escenario creado para él) no existe: se quita y se vuelve al activo anterior.
        await UpdateAsync(
            current => current with
            {
                Scenarios = created is not null
                    ? [.. current.Scenarios.Where(other => other.Id != created.Id)]
                    : [.. current.Scenarios.Select(other => other.Id == injected.ScenarioId
                        ? other with { Events = [.. other.Events.Where(e => e.Id != injected.Id)] }
                        : other)],
                ActiveScenarioId = created is not null && current.ActiveScenarioId == created.Id ? view.ActiveScenarioId : current.ActiveScenarioId,
                Error = StoreError,
            },
            ScenarioSignal.Recalculate);
        return null;
    }

    // Corre sin el token del pedido: una vez publicado, el evento tiene que quedar guardado aunque el cliente se vaya.
    private async Task<bool> SaveAsync(Scenario? created, InjectedEvent injected, int sessionKey)
    {
        try
        {
            await store.SaveAsync(created, injected, CancellationToken.None);
            return true;
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException)
        {
            LogStoreFailed(exception, sessionKey);
            return false;
        }
    }

    private Task SetErrorAsync(string error) => UpdateAsync(current => current with { Error = error }, null);

    /// <param name="signal">Pedido de recálculo para <see cref="StrategyWorker"/>; null si solo cambia el error.</param>
    private async Task UpdateAsync(Func<ScenarioView, ScenarioView> change, ScenarioSignal? signal)
    {
        ScenarioView view;
        lock (_gate)
        {
            view = _view = change(_view);
        }

        await PublishAsync(view, signal);
    }

    private async Task SetViewAsync(ScenarioView view)
    {
        lock (_gate)
        {
            _view = view;
        }

        await PublishAsync(view, ScenarioSignal.Recalculate);
    }

    private async Task PublishAsync(ScenarioView view, ScenarioSignal? signal)
    {
        if (signal is not null)
        {
            _signals.Writer.TryWrite(signal);
        }

        Changed?.Invoke();
        await hub.Clients.All.OnScenarioChanged(view);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Scenario storage unavailable for session {SessionKey}")]
    private partial void LogStoreFailed(Exception exception, int sessionKey);

    [LoggerMessage(Level = LogLevel.Information, Message = "Injected {Event} into scenario {Scenario} (immediate: {Immediate})")]
    private partial void LogInjected(string @event, string scenario, bool immediate);
}
