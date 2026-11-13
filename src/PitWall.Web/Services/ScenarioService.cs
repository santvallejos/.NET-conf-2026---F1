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
            await SetViewAsync(new ScenarioView(sessionKey, scenarios, real.Id, error), null);
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
            var scenario = await store.CreateAsync(sessionKey, trimmed, cancellationToken);
            await UpdateAsync(current => current with { Scenarios = [.. current.Scenarios, scenario], ActiveScenarioId = scenario.Id, Error = null }, null);
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

        await UpdateAsync(current => current with { ActiveScenarioId = scenarioId, Error = null }, null);
    }

    /// <summary>
    /// Persiste el evento en el escenario activo (o en uno nuevo si el activo es <c>Real</c>) con los defaults de
    /// <c>Scenarios:*</c>. Devuelve null y deja el motivo en <see cref="ScenarioView.Error"/> si no se pudo.
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
        try
        {
            var scenario = view.Active;
            if (scenario is null || scenario.IsReal)
            {
                scenario = await store.CreateAsync(sessionKey, ScenarioLabels.DefaultScenarioName(request.Type, request.Lap), cancellationToken);
            }

            var injected = await store.AddEventAsync(scenario.Id, request.Type, request.Lap, parameters, cancellationToken);
            var updated = scenario with { Events = [.. scenario.Events, injected] };
            var immediate = request.Lap <= state.LeaderLap;
            await UpdateAsync(
                current => current with
                {
                    Scenarios = [.. current.Scenarios.Where(other => other.Id != updated.Id), updated],
                    ActiveScenarioId = updated.Id,
                    Error = null,
                },
                immediate ? injected : null);
            LogInjected(injected.Describe(), updated.Name, immediate);
            return injected;
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException)
        {
            LogStoreFailed(exception, sessionKey);
            await SetErrorAsync(StoreError);
            return null;
        }
    }

    private Task SetErrorAsync(string error) => UpdateAsync(current => current with { Error = error }, null, signal: false);

    private async Task UpdateAsync(Func<ScenarioView, ScenarioView> change, InjectedEvent? injected, bool signal = true)
    {
        ScenarioView view;
        lock (_gate)
        {
            view = _view = change(_view);
        }

        await PublishAsync(view, injected, signal);
    }

    private async Task SetViewAsync(ScenarioView view, InjectedEvent? injected)
    {
        lock (_gate)
        {
            _view = view;
        }

        await PublishAsync(view, injected, signal: true);
    }

    private async Task PublishAsync(ScenarioView view, InjectedEvent? injected, bool signal)
    {
        if (signal)
        {
            _signals.Writer.TryWrite(new ScenarioSignal(injected));
        }

        Changed?.Invoke();
        await hub.Clients.All.OnScenarioChanged(view);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Scenario storage unavailable for session {SessionKey}")]
    private partial void LogStoreFailed(Exception exception, int sessionKey);

    [LoggerMessage(Level = LogLevel.Information, Message = "Injected {Event} into scenario {Scenario} (immediate: {Immediate})")]
    private partial void LogInjected(string @event, string scenario, bool immediate);
}
