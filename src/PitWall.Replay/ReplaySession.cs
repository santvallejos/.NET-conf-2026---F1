using Microsoft.EntityFrameworkCore;

using PitWall.Data;
using PitWall.Data.Entities;
using PitWall.Domain;

namespace PitWall.Replay;

/// <summary>
/// Datos de una sesión leídos de SQLite una sola vez al iniciarla y cacheados en memoria: el replay no vuelve a
/// consultar la base por tick. La telemetría (<c>CarData</c>, <c>Location</c>) no se carga: no forma parte de
/// <see cref="RaceState"/>.
/// </summary>
public sealed class ReplaySession
{
    private readonly IReadOnlyDictionary<int, DateTimeOffset> _lapStarts;

    private ReplaySession(
        int sessionKey,
        IReadOnlyList<DriverTimeline> drivers,
        IReadOnlyDictionary<int, DateTimeOffset> lapStarts,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        Timeline<WeatherState> weather,
        Timeline<RaceControlMessage> raceControl,
        Timeline<TrackStatus> track)
    {
        SessionKey = sessionKey;
        Drivers = drivers;
        _lapStarts = lapStarts;
        StartTime = startTime;
        EndTime = endTime;
        Weather = weather;
        RaceControl = raceControl;
        Track = track;
        LapCount = lapStarts.Keys.Max();
    }

    public int SessionKey { get; }

    public DateTimeOffset StartTime { get; }

    public DateTimeOffset EndTime { get; }

    public int LapCount { get; }

    public IReadOnlyList<int> DriverNumbers => [.. Drivers.Select(driver => driver.DriverNumber)];

    internal static TrackStatus GreenTrack { get; } = new("GREEN", SafetyCar: false, VirtualSafetyCar: false);

    internal IReadOnlyList<DriverTimeline> Drivers { get; }

    internal Timeline<WeatherState> Weather { get; }

    internal Timeline<RaceControlMessage> RaceControl { get; }

    internal Timeline<TrackStatus> Track { get; }

    /// <summary>Instante en que el primer auto (el líder) inicia la vuelta <paramref name="lap"/>.</summary>
    public DateTimeOffset GetLapStartTime(int lap)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(lap, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(lap, LapCount);

        return _lapStarts.TryGetValue(lap, out var start)
            ? start
            : throw new ArgumentOutOfRangeException(nameof(lap), lap, $"Session {SessionKey} has no start time for lap {lap}.");
    }

    public static async Task<ReplaySession> LoadAsync(PitWallDbContext db, int sessionKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        var driverNumbers = await db.Drivers.AsNoTracking().Where(e => e.SessionKey == sessionKey).Select(e => e.DriverNumber).ToListAsync(cancellationToken);
        var laps = await db.Laps.AsNoTracking().Where(e => e.SessionKey == sessionKey).ToListAsync(cancellationToken);
        var stints = await db.Stints.AsNoTracking().Where(e => e.SessionKey == sessionKey).ToListAsync(cancellationToken);
        var pitStops = await db.PitStops.AsNoTracking().Where(e => e.SessionKey == sessionKey).ToListAsync(cancellationToken);
        var positions = await db.Positions.AsNoTracking().Where(e => e.SessionKey == sessionKey).ToListAsync(cancellationToken);
        var intervals = await db.Intervals.AsNoTracking().Where(e => e.SessionKey == sessionKey).ToListAsync(cancellationToken);
        var grid = await db.StartingGrid.AsNoTracking().Where(e => e.SessionKey == sessionKey).ToDictionaryAsync(e => e.DriverNumber, e => e.Position, cancellationToken);
        var weather = await db.Weather.AsNoTracking().Where(e => e.SessionKey == sessionKey).ToListAsync(cancellationToken);
        var raceControl = await db.RaceControl.AsNoTracking().Where(e => e.SessionKey == sessionKey).ToListAsync(cancellationToken);

        var startedLaps = laps.Where(lap => lap.DateStart is not null).ToList();
        if (startedLaps.Count == 0)
        {
            throw new ReplaySessionException($"Session {sessionKey} has no laps with a start time; run the ingestion first.");
        }

        var lapStarts = startedLaps
            .GroupBy(lap => lap.LapNumber)
            .ToDictionary(group => group.Key, group => group.Min(lap => lap.DateStart!.Value));
        var startTime = lapStarts.TryGetValue(1, out var firstLapStart) ? firstLapStart : lapStarts.Values.Min();
        DateTimeOffset[] endCandidates =
        [
            startedLaps.Max(lap => lap.DateStart!.Value + TimeSpan.FromSeconds(lap.Duration ?? 0)),
            positions.Select(e => e.Date).DefaultIfEmpty(startTime).Max(),
            intervals.Select(e => e.Date).DefaultIfEmpty(startTime).Max(),
            raceControl.Select(e => e.Date).DefaultIfEmpty(startTime).Max(),
        ];

        var drivers = driverNumbers
            .Union(laps.Select(lap => lap.DriverNumber))
            .Order()
            .Select(number => new DriverTimeline(
                number,
                grid.TryGetValue(number, out var gridPosition) ? gridPosition : null,
                laps.Where(e => e.DriverNumber == number),
                stints.Where(e => e.DriverNumber == number),
                pitStops.Where(e => e.DriverNumber == number),
                positions.Where(e => e.DriverNumber == number),
                intervals.Where(e => e.DriverNumber == number)))
            .ToList();

        var orderedRaceControl = raceControl.OrderBy(e => e.Date).ThenBy(e => e.Id).ToList();

        return new ReplaySession(
            sessionKey,
            drivers,
            lapStarts,
            startTime,
            endCandidates.Max(),
            new Timeline<WeatherState>(weather.Select(e => (e.Date, new WeatherState(e.AirTemp, e.TrackTemp, e.Humidity, e.Rainfall)))),
            new Timeline<RaceControlMessage>(orderedRaceControl.Select(e => (e.Date, ToMessage(e)))),
            new Timeline<TrackStatus>(BuildTrackChanges(orderedRaceControl)));
    }

    private static RaceControlMessage ToMessage(RaceControl entity) =>
        new(entity.Date, entity.LapNumber, entity.Category, entity.Flag, entity.Scope, entity.DriverNumber, entity.Message);

    /// <summary>
    /// Estado de pista derivado de Race Control: la bandera es la última de alcance <c>Track</c>; SC y VSC se
    /// activan con su mensaje "DEPLOYED", el VSC termina con "VIRTUAL SAFETY CAR ENDING" y ambos con bandera verde.
    /// </summary>
    private static List<(DateTimeOffset, TrackStatus)> BuildTrackChanges(IEnumerable<RaceControl> messages)
    {
        var changes = new List<(DateTimeOffset, TrackStatus)>();
        var current = GreenTrack;
        foreach (var message in messages)
        {
            var next = current;
            if (string.Equals(message.Category, "Flag", StringComparison.OrdinalIgnoreCase)
                && string.Equals(message.Scope, "Track", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(message.Flag))
            {
                var flag = message.Flag.ToUpperInvariant();
                next = flag == "GREEN" ? GreenTrack : next with { Flag = flag };
            }
            else if (string.Equals(message.Category, "SafetyCar", StringComparison.OrdinalIgnoreCase))
            {
                // "VIRTUAL SAFETY CAR DEPLOYED" contiene "SAFETY CAR DEPLOYED": el VSC se evalúa primero.
                var text = message.Message.ToUpperInvariant();
                if (text.Contains("VIRTUAL SAFETY CAR DEPLOYED", StringComparison.Ordinal))
                {
                    next = next with { VirtualSafetyCar = true };
                }
                else if (text.Contains("VIRTUAL SAFETY CAR ENDING", StringComparison.Ordinal))
                {
                    next = next with { VirtualSafetyCar = false };
                }
                else if (text.Contains("SAFETY CAR DEPLOYED", StringComparison.Ordinal))
                {
                    next = next with { SafetyCar = true };
                }
            }

            if (next != current)
            {
                current = next;
                changes.Add((message.Date, current));
            }
        }

        return changes;
    }
}
