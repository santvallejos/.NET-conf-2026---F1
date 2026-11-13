using Microsoft.EntityFrameworkCore;

using PitWall.Data;
using PitWall.Domain;
using PitWall.Strategy.Workers;
using PitWall.Web.Components.Panels.Tyres;

namespace PitWall.Web.Services;

/// <summary>
/// Datos de una sesión que necesita el cálculo de estrategia y que <see cref="RaceState"/> no trae (vueltas
/// clasificadas, paradas y Race Control). Se leen de SQLite una vez por sesión; cada
/// contexto solo usa lo ocurrido hasta el reloj del replay.
/// </summary>
internal sealed class StrategySessionData
{
    private readonly TyreSessionLaps _laps;
    private readonly (DateTimeOffset Date, double LaneDuration)[] _pitStops;
    private readonly RaceControlMessage[] _raceControl;

    private StrategySessionData(
        int sessionKey,
        TyreSessionLaps laps,
        (DateTimeOffset, double)[] pitStops,
        RaceControlMessage[] raceControl)
    {
        SessionKey = sessionKey;
        _laps = laps;
        _pitStops = pitStops;
        _raceControl = raceControl;
    }

    public int SessionKey { get; }

    public int TotalLaps => _laps.LapCount;

    public static StrategySessionData Empty(int sessionKey) => new(sessionKey, TyreSessionLaps.Empty(sessionKey), [], []);

    public static async Task<StrategySessionData> LoadAsync(PitWallDbContext db, int sessionKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        var laps = await TyreSessionLaps.LoadAsync(db, sessionKey, cancellationToken);
        var pitStops = await db.PitStops.AsNoTracking()
            .Where(e => e.SessionKey == sessionKey && e.LaneDuration != null)
            .Select(e => new { e.Date, e.LaneDuration })
            .ToListAsync(cancellationToken);
        var raceControl = await db.RaceControl.AsNoTracking().Where(e => e.SessionKey == sessionKey).ToListAsync(cancellationToken);

        return new StrategySessionData(
            sessionKey,
            laps,
            [.. pitStops.OrderBy(e => e.Date).Select(e => (e.Date, e.LaneDuration!.Value))],
            [.. raceControl
                .OrderBy(e => e.Date)
                .ThenBy(e => e.Id)
                .Select(e => new RaceControlMessage(e.Date, e.LapNumber, e.Category, e.Flag, e.Scope, e.DriverNumber, e.Message))]);
    }

    /// <summary>Contexto de estrategia en el instante de <paramref name="state"/>; null si la sesión no tiene vueltas.</summary>
    public StrategyContext? Context(RaceState state, IReadOnlyDictionary<int, EnergyEstimate> energy)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(energy);

        if (state.SessionKey != SessionKey || TotalLaps == 0)
        {
            return null;
        }

        var clock = state.Clock;
        return new StrategyContext(
            state,
            TotalLaps,
            _laps.Completed(_laps.CompletedCount(clock)),
            [.. _raceControl.TakeWhile(message => message.Date <= clock)],
            [.. _pitStops.TakeWhile(stop => stop.Date <= clock).Select(stop => stop.LaneDuration)],
            energy);
    }
}
