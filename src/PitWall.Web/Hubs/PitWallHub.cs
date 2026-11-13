using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

using PitWall.Domain;
using PitWall.Jev;
using PitWall.Jev.Runtime;
using PitWall.Strategy.Scenarios;
using PitWall.Web.Services;

namespace PitWall.Web.Hubs;

/// <summary>
/// Hub del pit wall: empuja los deltas de <see cref="RaceState"/> y el estado del reproductor a todos los clientes
/// y expone los mismos controles que el panel Controles. Todos los clientes comparten la sesión activa.
/// </summary>
public sealed class PitWallHub(
    PitWallSession session,
    StrategyStore strategies,
    JevDecisionStore jevDecisions,
    JevTriggers jevTriggers,
    JevRuntimeOverrides jevOverrides,
    IOptionsMonitor<JevOptions> jevOptions,
    ScenarioService scenarios,
    ScenarioStrategyStore hypotheticalStrategies) : Hub<IPitWallClient>
{
    public const string Route = "/hubs/pitwall";

    public override async Task OnConnectedAsync()
    {
        if (session.GetSnapshot() is { } snapshot)
        {
            await Clients.Caller.OnRaceStateDelta(snapshot);
        }

        await Clients.Caller.OnPlaybackChanged(session.View.Playback);
        await base.OnConnectedAsync();
    }

    public IReadOnlyList<SessionInfo> GetSessions() => session.View.Sessions;

    public RaceStateDelta? GetSnapshot() => session.GetSnapshot();

    /// <summary>Última estrategia calculada del piloto en la sesión activa, o null.</summary>
    public StrategySnapshot? GetStrategy(int driverNumber) =>
        session.View.State is { } state ? strategies.Get(state.SessionKey, driverNumber)?.Snapshot : null;

    /// <summary>Mensajes de Race Control hasta el instante actual, del más nuevo al más viejo.</summary>
    public IReadOnlyList<RaceControlMessage> GetRaceControl() => session.View.RaceControl;

    [HubMethodName("LoadSession")]
    public Task LoadSessionAsync(int sessionKey) => session.LoadSessionAsync(sessionKey, Context.ConnectionAborted);

    [HubMethodName("Play")]
    public Task PlayAsync() => session.PlayAsync();

    [HubMethodName("Pause")]
    public Task PauseAsync() => session.PauseAsync();

    [HubMethodName("SetSpeed")]
    public Task SetSpeedAsync(int speed) => session.SetSpeedAsync(speed);

    [HubMethodName("JumpToLap")]
    public Task JumpToLapAsync(int lap) => session.JumpToLapAsync(lap);

    /// <summary>Decisiones de Jev de la sesión activa, de la más vieja a la más nueva.</summary>
    public IReadOnlyList<JevDecision> GetJevDecisions() =>
        session.View.State is { } state ? jevDecisions.Get(state.SessionKey) : [];

    /// <summary>Configuración de Jev vigente, con los cambios en caliente aplicados.</summary>
    public JevSettings GetJevSettings() => JevSettings.From(jevOptions.CurrentValue);

    /// <summary>"Consultar Jev": estrategia del piloto ahora, en cualquier modo; la decisión llega por <c>OnJevDecision</c>.</summary>
    public void RequestJevStrategy(int driverNumber) => jevTriggers.RequestStrategy(driverNumber);

    public JevSettings SetJevEnabled(bool enabled)
    {
        jevOverrides.SetEnabled(enabled);
        return GetJevSettings();
    }

    public JevSettings SetJevMode(JevMode mode)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new HubException($"Unknown Jev mode {mode}.");
        }

        jevOverrides.SetMode(mode);
        return GetJevSettings();
    }

    public JevSettings SetJevLaps(int[] laps)
    {
        jevOverrides.SetLaps(laps);
        return GetJevSettings();
    }

    public JevSettings SetJevDriverEnabled(int driverNumber, bool enabled)
    {
        jevOverrides.SetDriverEnabled(driverNumber, enabled);
        return GetJevSettings();
    }

    /// <summary>Escenarios de la sesión activa (<c>Real</c> siempre está) y el activo.</summary>
    public ScenarioView GetScenarios() => scenarios.View;

    /// <summary>Crea un escenario vacío y lo deja activo; si no se pudo, el motivo viene en <c>error</c>.</summary>
    [HubMethodName("CreateScenario")]
    public async Task<ScenarioView> CreateScenarioAsync(string name)
    {
        await scenarios.CreateAsync(name, Context.ConnectionAborted);
        return scenarios.View;
    }

    [HubMethodName("SelectScenario")]
    public async Task<ScenarioView> SelectScenarioAsync(Guid scenarioId)
    {
        await scenarios.SelectAsync(scenarioId);
        return scenarios.View;
    }

    /// <summary>
    /// Inyecta un evento hipotético en el escenario activo (o en uno nuevo si el activo es <c>Real</c>). Con
    /// <c>lap</c> ≤ vuelta actual tiene efecto inmediato; si no, queda programado. null si no se pudo.
    /// </summary>
    [HubMethodName("InjectEvent")]
    public Task<InjectedEvent?> InjectEventAsync(InjectEventRequest request) => scenarios.InjectAsync(request, Context.ConnectionAborted);

    /// <summary>Última estrategia HIPOTÉTICA del piloto en el escenario activo, o null.</summary>
    public StrategySnapshot? GetHypotheticalStrategy(int driverNumber) =>
        session.View.State is { } state && scenarios.View.Active is { IsReal: false } scenario
            ? hypotheticalStrategies.Get(state.SessionKey, scenario.Id, driverNumber)?.Publication.Result.Snapshot
            : null;
}
