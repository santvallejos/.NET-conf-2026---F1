using Microsoft.AspNetCore.SignalR;

using PitWall.Domain;
using PitWall.Web.Services;

namespace PitWall.Web.Hubs;

/// <summary>
/// Hub del pit wall: empuja los deltas de <see cref="RaceState"/> y el estado del reproductor a todos los clientes
/// y expone los mismos controles que el panel Controles. Todos los clientes comparten la sesión activa.
/// </summary>
public sealed class PitWallHub(PitWallSession session) : Hub<IPitWallClient>
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
}
