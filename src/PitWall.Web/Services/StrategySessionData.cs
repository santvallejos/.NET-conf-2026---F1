using Microsoft.EntityFrameworkCore;

using PitWall.Data;
using PitWall.Domain;
using PitWall.Strategy.Degradation;
using PitWall.Strategy.Workers;
using PitWall.Web.Components.Panels.Tyres;

namespace PitWall.Web.Services;

/// <summary>
/// Datos de una sesión que necesita el cálculo de estrategia y que <see cref="RaceState"/> no trae (vueltas
/// clasificadas, paradas, adelantamientos, Race Control y lluvia). Se leen de SQLite una vez por sesión; cada
/// contexto solo usa lo ocurrido hasta el reloj del replay.
/// </summary>
internal sealed class StrategySessionData
{
    // ESTIMADO: OpenF1 no informa si la pista sigue mojada; se asume hasta 10 minutos después de la última lluvia.
    private static readonly TimeSpan TrackWetWindow = TimeSpan.FromMinutes(10);

    // "Vuelta anterior" para la lluvia previa si aún no hay vueltas con tiempo.
    private static readonly TimeSpan DefaultLapDuration = TimeSpan.FromSeconds(90);

    private const int LapsForLookback = 20;

    private readonly TyreSessionLaps _laps;
    private readonly (DateTimeOffset Date, double LaneDuration)[] _pitStops;
    private readonly DateTimeOffset[] _overtakes;
    private readonly RaceControlMessage[] _raceControl;
    private readonly (DateTimeOffset Date, bool Rainfall)[] _weather;

    private StrategySessionData(
        int sessionKey,
        TyreSessionLaps laps,
        (DateTimeOffset, double)[] pitStops,
        DateTimeOffset[] overtakes,
        RaceControlMessage[] raceControl,
        (DateTimeOffset, bool)[] weather)
    {
        SessionKey = sessionKey;
        _laps = laps;
        _pitStops = pitStops;
        _overtakes = overtakes;
        _raceControl = raceControl;
        _weather = weather;
    }

    public int SessionKey { get; }

    public int TotalLaps => _laps.LapCount;

    public static StrategySessionData Empty(int sessionKey) => new(sessionKey, TyreSessionLaps.Empty(sessionKey), [], [], [], []);

    public static async Task<StrategySessionData> LoadAsync(PitWallDbContext db, int sessionKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        var laps = await TyreSessionLaps.LoadAsync(db, sessionKey, cancellationToken);
        var pitStops = await db.PitStops.AsNoTracking()
            .Where(e => e.SessionKey == sessionKey && e.LaneDuration != null)
            .Select(e => new { e.Date, e.LaneDuration })
            .ToListAsync(cancellationToken);
        var overtakes = await db.Overtakes.AsNoTracking().Where(e => e.SessionKey == sessionKey).Select(e => e.Date).ToListAsync(cancellationToken);
        var raceControl = await db.RaceControl.AsNoTracking().Where(e => e.SessionKey == sessionKey).ToListAsync(cancellationToken);
        var weather = await db.Weather.AsNoTracking()
            .Where(e => e.SessionKey == sessionKey)
            .Select(e => new { e.Date, e.Rainfall })
            .ToListAsync(cancellationToken);

        return new StrategySessionData(
            sessionKey,
            laps,
            [.. pitStops.OrderBy(e => e.Date).Select(e => (e.Date, e.LaneDuration!.Value))],
            [.. overtakes.Order()],
            [.. raceControl
                .OrderBy(e => e.Date)
                .ThenBy(e => e.Id)
                .Select(e => new RaceControlMessage(e.Date, e.LapNumber, e.Category, e.Flag, e.Scope, e.DriverNumber, e.Message))],
            [.. weather.OrderBy(e => e.Date).Select(e => (e.Date, e.Rainfall))]);
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
        var completed = _laps.Completed(_laps.CompletedCount(clock));
        var used = state.Cars.ToDictionary(
            car => car.DriverNumber,
            car => (IReadOnlyList<Compound>)[.. _laps.StintsUpTo(car.DriverNumber, car.Lap).Select(stint => stint.Compound).Distinct()]);

        return new StrategyContext(
            state,
            TotalLaps,
            completed,
            used,
            [.. _raceControl.TakeWhile(message => message.Date <= clock)],
            RainfallAt(clock - LapDuration(completed)),
            _weather.Any(reading => reading.Rainfall && reading.Date <= clock && reading.Date > clock - TrackWetWindow),
            [.. _pitStops.TakeWhile(stop => stop.Date <= clock).Select(stop => stop.LaneDuration)],
            _overtakes.TakeWhile(date => date <= clock).Count(),
            energy,
            new HashSet<int>());
    }

    private bool RainfallAt(DateTimeOffset date)
    {
        var rainfall = false;
        foreach (var reading in _weather)
        {
            if (reading.Date > date)
            {
                break;
            }

            rainfall = reading.Rainfall;
        }

        return rainfall;
    }

    // Mediana de las últimas vueltas cerradas de la parrilla: el instante "una vuelta atrás".
    private static TimeSpan LapDuration(IReadOnlyList<LapSample> completed)
    {
        var times = completed
            .Skip(Math.Max(0, completed.Count - LapsForLookback))
            .Select(lap => lap.LapTime)
            .OfType<double>()
            .Where(time => time > 0)
            .Order()
            .ToList();
        return times.Count == 0 ? DefaultLapDuration : TimeSpan.FromSeconds(times[times.Count / 2]);
    }
}
