using System.Globalization;
using System.Text;

using Microsoft.EntityFrameworkCore;

using PitWall.Data;

namespace PitWall.Web.Components.Panels.Map;

/// <summary>
/// Trazado del circuito y posiciones (<c>location</c>) de los pilotos ingeridos, ya en coordenadas del SVG. Se lee
/// de SQLite una vez por sesión.
/// </summary>
internal sealed class TrackMapSessionData
{
    private const double ViewSize = 1000;
    private const double Padding = 40;
    private const int MinOutlinePoints = 50;
    private const int MaxFallbackPoints = 2000;

    private readonly IReadOnlyDictionary<int, (DateTimeOffset[] Dates, TrackPoint[] Points)> _locations;

    private TrackMapSessionData(
        int sessionKey,
        double width,
        double height,
        string outline,
        TrackPoint? startFinish,
        IReadOnlyDictionary<int, (DateTimeOffset[] Dates, TrackPoint[] Points)> locations)
    {
        SessionKey = sessionKey;
        Width = width;
        Height = height;
        Outline = outline;
        StartFinish = startFinish;
        _locations = locations;
    }

    public int SessionKey { get; }

    public double Width { get; }

    public double Height { get; }

    /// <summary>Atributo <c>points</c> de la polyline del trazado; vacío si no hay <c>location</c>.</summary>
    public string Outline { get; }

    public TrackPoint? StartFinish { get; }

    public bool IsEmpty => Outline.Length == 0;

    public static TrackMapSessionData Empty(int sessionKey) =>
        new(sessionKey, ViewSize, ViewSize, string.Empty, null, new Dictionary<int, (DateTimeOffset[], TrackPoint[])>());

    /// <summary>Pilotos con <c>location</c> y su última posición con fecha en (<paramref name="clock"/> − <paramref name="staleAfter"/>, <paramref name="clock"/>].</summary>
    public IReadOnlyList<TrackMapCar> CarsAt(DateTimeOffset clock, TimeSpan staleAfter)
    {
        var cars = new List<TrackMapCar>(_locations.Count);
        foreach (var (driverNumber, (dates, points)) in _locations)
        {
            var index = Array.BinarySearch(dates, clock);
            index = index >= 0 ? index : ~index - 1;
            if (index >= 0 && clock - dates[index] <= staleAfter)
            {
                cars.Add(new TrackMapCar(driverNumber, points[index]));
            }
        }

        return cars;
    }

    public static async Task<TrackMapSessionData> LoadAsync(PitWallDbContext db, int sessionKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        // OpenF1 manda (0, 0, 0) cuando no tiene posición: no son puntos del circuito.
        var samples = await db.Locations.AsNoTracking()
            .Where(e => e.SessionKey == sessionKey && (e.X != 0 || e.Y != 0))
            .Select(e => new { e.DriverNumber, e.Date, e.X, e.Y })
            .ToListAsync(cancellationToken);
        if (samples.Count == 0)
        {
            return Empty(sessionKey);
        }

        var laps = await db.Laps.AsNoTracking()
            .Where(e => e.SessionKey == sessionKey && e.DateStart != null && e.Duration != null)
            .Select(e => new { e.DriverNumber, e.LapNumber, e.DateStart, e.Duration, e.IsPitOutLap })
            .ToListAsync(cancellationToken);

        var byDriver = samples
            .GroupBy(e => e.DriverNumber)
            .ToDictionary(group => group.Key, group => group.OrderBy(e => e.Date).Select(e => (e.Date, e.X, e.Y)).ToArray());

        // El trazado sale de la vuelta limpia más rápida con location (sin salida ni entrada a boxes): sigue la
        // línea de carrera y no dibuja el pit lane. Sin ninguna, se usa la traza completa del piloto con más datos.
        var pitOutLaps = laps.Where(e => e.IsPitOutLap).Select(e => (e.DriverNumber, e.LapNumber)).ToHashSet();
        (DateTimeOffset Date, int X, int Y)[] outline = [];
        foreach (var lap in laps
            .Where(e => e.LapNumber > 1 && !e.IsPitOutLap && !pitOutLaps.Contains((e.DriverNumber, e.LapNumber + 1)))
            .Where(e => byDriver.ContainsKey(e.DriverNumber))
            .OrderBy(e => e.Duration))
        {
            var start = lap.DateStart!.Value;
            var end = start.AddSeconds(lap.Duration!.Value);
            outline = [.. byDriver[lap.DriverNumber].Where(e => e.Date >= start && e.Date <= end)];
            if (outline.Length >= MinOutlinePoints)
            {
                break;
            }
        }

        if (outline.Length < MinOutlinePoints)
        {
            var longest = byDriver.Values.MaxBy(trace => trace.Length)!;
            var stride = Math.Max(1, longest.Length / MaxFallbackPoints);
            outline = [.. longest.Where((_, index) => index % stride == 0)];
        }

        var minX = outline.Min(e => e.X);
        var maxX = outline.Max(e => e.X);
        var minY = outline.Min(e => e.Y);
        var maxY = outline.Max(e => e.Y);
        var scale = ViewSize / Math.Max(1, Math.Max(maxX - minX, maxY - minY));

        // OpenF1 usa y hacia arriba; el SVG, hacia abajo.
        TrackPoint Project(int x, int y) => new(Padding + ((x - minX) * scale), Padding + ((maxY - y) * scale));

        var points = new StringBuilder(outline.Length * 12);
        foreach (var (_, x, y) in outline)
        {
            var point = Project(x, y);
            points.Append(CultureInfo.InvariantCulture, $"{point.X:0.#},{point.Y:0.#} ");
        }

        return new TrackMapSessionData(
            sessionKey,
            ((maxX - minX) * scale) + (2 * Padding),
            ((maxY - minY) * scale) + (2 * Padding),
            points.ToString(),
            Project(outline[0].X, outline[0].Y),
            byDriver.ToDictionary(
                pair => pair.Key,
                pair => (pair.Value.Select(e => e.Date).ToArray(), pair.Value.Select(e => Project(e.X, e.Y)).ToArray())));
    }
}
