using System.Text.RegularExpressions;

using Microsoft.EntityFrameworkCore;

using PitWall.Data;
using PitWall.Domain;

namespace PitWall.Web.Services;

/// <summary>
/// Datos de presentación de una sesión que <see cref="RaceState"/> no trae (nombres y colores, historial de Race
/// Control, paradas, viento y serie de clima). Se leen de SQLite una vez al cargar la sesión.
/// </summary>
internal sealed partial class SessionDetails
{
    private const string NeutralColour = "#8A8F98";

    private readonly RaceControlMessage[] _raceControl;
    private readonly DateTimeOffset[] _raceControlDates;
    private readonly IReadOnlyDictionary<int, DateTimeOffset[]> _pitStops;
    private readonly WeatherReading[] _weather;
    private readonly DateTimeOffset[] _weatherDates;

    private SessionDetails(
        int sessionKey,
        IReadOnlyDictionary<int, DriverInfo> drivers,
        RaceControlMessage[] raceControl,
        IReadOnlyDictionary<int, DateTimeOffset[]> pitStops,
        WeatherReading[] weather)
    {
        SessionKey = sessionKey;
        Drivers = drivers;
        _raceControl = raceControl;
        _raceControlDates = [.. raceControl.Select(message => message.Date)];
        _pitStops = pitStops;
        _weather = weather;
        _weatherDates = [.. weather.Select(reading => reading.Date)];
    }

    public int SessionKey { get; }

    public IReadOnlyDictionary<int, DriverInfo> Drivers { get; }

    /// <summary>Mensajes de Race Control con fecha ≤ <paramref name="clock"/>, en orden cronológico.</summary>
    public ArraySegment<RaceControlMessage> RaceControlUpTo(DateTimeOffset clock) =>
        new(_raceControl, 0, CountUpTo(_raceControlDates, clock));

    public IReadOnlyList<WeatherReading> WeatherUpTo(DateTimeOffset clock) =>
        new ArraySegment<WeatherReading>(_weather, 0, CountUpTo(_weatherDates, clock));

    public IReadOnlyDictionary<int, int> PitStopsUpTo(DateTimeOffset clock) =>
        _pitStops.ToDictionary(pair => pair.Key, pair => CountUpTo(pair.Value, clock));

    public static async Task<SessionDetails> LoadAsync(PitWallDbContext db, int sessionKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        var drivers = await db.Drivers.AsNoTracking().Where(e => e.SessionKey == sessionKey).ToListAsync(cancellationToken);
        var raceControl = await db.RaceControl.AsNoTracking().Where(e => e.SessionKey == sessionKey).ToListAsync(cancellationToken);
        var pitStops = await db.PitStops.AsNoTracking().Where(e => e.SessionKey == sessionKey).ToListAsync(cancellationToken);
        var weather = await db.Weather.AsNoTracking().Where(e => e.SessionKey == sessionKey).ToListAsync(cancellationToken);

        return new SessionDetails(
            sessionKey,
            drivers.ToDictionary(
                e => e.DriverNumber,
                e => new DriverInfo(e.DriverNumber, e.Acronym, e.FullName, e.Team, ToCssColour(e.TeamColour))),
            [.. raceControl
                .OrderBy(e => e.Date)
                .ThenBy(e => e.Id)
                .Select(e => new RaceControlMessage(e.Date, e.LapNumber, e.Category, e.Flag, e.Scope, e.DriverNumber, e.Message))],
            pitStops
                .GroupBy(e => e.DriverNumber)
                .ToDictionary(group => group.Key, group => group.Select(e => e.Date).Order().ToArray()),
            [.. weather
                .OrderBy(e => e.Date)
                .Select(e => new WeatherReading(e.Date, e.AirTemp, e.TrackTemp, e.Humidity, e.Rainfall, e.WindSpeed, e.WindDirection))]);
    }

    // El color va a un atributo style: solo se acepta hex de 6 dígitos (OpenF1 lo manda sin '#').
    private static string ToCssColour(string teamColour)
    {
        var colour = teamColour.Trim().TrimStart('#');
        return HexColourPattern().IsMatch(colour) ? $"#{colour}" : NeutralColour;
    }

    private static int CountUpTo(DateTimeOffset[] dates, DateTimeOffset clock)
    {
        var low = 0;
        var high = dates.Length;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (dates[middle] <= clock)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    [GeneratedRegex("^[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColourPattern();
}
