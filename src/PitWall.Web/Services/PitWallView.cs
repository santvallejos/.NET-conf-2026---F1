using PitWall.Domain;

namespace PitWall.Web.Services;

/// <summary>
/// Instantánea inmutable de lo que muestran los paneles; se rearma con cada estado del replay para que todos
/// los paneles rendericen el mismo instante.
/// </summary>
public sealed record PitWallView(
    IReadOnlyList<SessionInfo> Sessions,
    PlaybackState Playback,
    RaceState? State,
    IReadOnlyDictionary<int, DriverInfo> Drivers,
    IReadOnlyDictionary<int, int> PitStops,
    IReadOnlyList<RaceControlMessage> RaceControl,
    FlagBoard Flags,
    IReadOnlyList<WeatherReading> WeatherHistory,
    int FocusDriver,
    string? Error)
{
    /// <summary>Tiempo de carrera transcurrido desde la largada de la sesión cargada.</summary>
    public TimeSpan Elapsed =>
        State is not null && Playback.StartTime is { } start && State.Clock > start ? State.Clock - start : TimeSpan.Zero;

    public SessionInfo? CurrentSession => Sessions.FirstOrDefault(session => session.SessionKey == Playback.SessionKey);
}
