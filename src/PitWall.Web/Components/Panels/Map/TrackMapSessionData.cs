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
    // Margen suficiente para que las etiquetas de sector del borde no queden recortadas.
    private const double Padding = 60;
    private const int MinOutlinePoints = 50;
    private const int MaxFallbackPoints = 2000;
    private const double SectorHalfLength = 22;
    private const double SectorLabelOffset = 42;
    private const int TangentSpan = 2;

    private readonly IReadOnlyDictionary<int, (DateTimeOffset[] Dates, TrackPoint[] Points)> _locations;

    private TrackMapSessionData(
        int sessionKey,
        double width,
        double height,
        string outline,
        IReadOnlyList<TrackMapSector> sectors,
        IReadOnlyDictionary<int, (DateTimeOffset[] Dates, TrackPoint[] Points)> locations)
    {
        SessionKey = sessionKey;
        Width = width;
        Height = height;
        Outline = outline;
        Sectors = sectors;
        _locations = locations;
    }

    public int SessionKey { get; }

    public double Width { get; }

    public double Height { get; }

    /// <summary>Atributo <c>points</c> de la polyline del trazado; vacío si no hay <c>location</c>.</summary>
    public string Outline { get; }

    /// <summary>Inicio de S1 (largada) y, si la vuelta del trazado tiene tiempos de sector, de S2 y S3.</summary>
    public IReadOnlyList<TrackMapSector> Sectors { get; }

    public bool IsEmpty => Outline.Length == 0;

    public static TrackMapSessionData Empty(int sessionKey) =>
        new(sessionKey, ViewSize, ViewSize, string.Empty, [], new Dictionary<int, (DateTimeOffset[], TrackPoint[])>());

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
            .Select(e => new { e.DriverNumber, e.LapNumber, e.DateStart, e.Duration, e.S1, e.S2, e.IsPitOutLap })
            .ToListAsync(cancellationToken);

        var byDriver = samples
            .GroupBy(e => e.DriverNumber)
            .ToDictionary(group => group.Key, group => group.OrderBy(e => e.Date).Select(e => (e.Date, e.X, e.Y)).ToArray());

        // El trazado sale de la vuelta limpia más rápida con location (sin salida ni entrada a boxes): sigue la
        // línea de carrera y no dibuja el pit lane. Sin ninguna, se usa la traza completa del piloto con más datos.
        var pitOutLaps = laps.Where(e => e.IsPitOutLap).Select(e => (e.DriverNumber, e.LapNumber)).ToHashSet();
        (DateTimeOffset Date, int X, int Y)[] outline = [];
        DateTimeOffset? sector2Start = null;
        DateTimeOffset? sector3Start = null;
        foreach (var lap in laps
            .Where(e => e.LapNumber > 1 && !e.IsPitOutLap && !pitOutLaps.Contains((e.DriverNumber, e.LapNumber + 1)))
            .Where(e => byDriver.ContainsKey(e.DriverNumber))
            .OrderBy(e => e.Duration))
        {
            var start = lap.DateStart!.Value;
            var duration = lap.Duration!.Value;
            var end = start.AddSeconds(duration);
            outline = [.. byDriver[lap.DriverNumber].Where(e => e.Date >= start && e.Date <= end)];
            if (outline.Length >= MinOutlinePoints)
            {
                // Los límites de sector salen de los tiempos de sector de esta misma vuelta.
                if (lap.S1 is { } s1 && s1 > 0 && s1 < duration)
                {
                    sector2Start = start.AddSeconds(s1);
                    if (lap.S2 is { } s2 && s2 > 0 && s1 + s2 < duration)
                    {
                        sector3Start = start.AddSeconds(s1 + s2);
                    }
                }

                break;
            }
        }

        if (outline.Length < MinOutlinePoints)
        {
            // La traza completa no es una vuelta: no hay límites de sector que ubicar.
            sector2Start = null;
            sector3Start = null;
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

        var track = outline.Select(e => Project(e.X, e.Y)).ToArray();
        var points = new StringBuilder(track.Length * 12);
        foreach (var point in track)
        {
            points.Append(CultureInfo.InvariantCulture, $"{point.X:0.#},{point.Y:0.#} ");
        }

        var width = ((maxX - minX) * scale) + (2 * Padding);
        var height = ((maxY - minY) * scale) + (2 * Padding);
        var center = new TrackPoint(width / 2, height / 2);
        int NearestIndex(DateTimeOffset instant) =>
            Enumerable.Range(0, outline.Length).MinBy(index => (outline[index].Date - instant).Duration());

        List<TrackMapSector> sectors = [Sector("S1", track, 0, center)];
        if (sector2Start is { } s2Start)
        {
            sectors.Add(Sector("S2", track, NearestIndex(s2Start), center));
        }

        if (sector3Start is { } s3Start)
        {
            sectors.Add(Sector("S3", track, NearestIndex(s3Start), center));
        }

        return new TrackMapSessionData(
            sessionKey,
            width,
            height,
            points.ToString(),
            sectors,
            byDriver.ToDictionary(
                pair => pair.Key,
                pair => (pair.Value.Select(e => e.Date).ToArray(), pair.Value.Select(e => Project(e.X, e.Y)).ToArray())));
    }

    /// <summary>
    /// Barra perpendicular al trazado en <paramref name="index"/>: la tangente sale de los puntos vecinos y la
    /// etiqueta va sobre la normal, del lado opuesto al centro del mapa (afuera del circuito).
    /// </summary>
    private static TrackMapSector Sector(string label, TrackPoint[] track, int index, TrackPoint center)
    {
        var from = track[Math.Max(0, index - TangentSpan)];
        var to = track[Math.Min(track.Length - 1, index + TangentSpan)];
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        var (nx, ny) = length > 0 ? (-dy / length, dx / length) : (0d, 1d);

        var point = track[index];
        if (((point.X - center.X) * nx) + ((point.Y - center.Y) * ny) < 0)
        {
            (nx, ny) = (-nx, -ny);
        }

        return new TrackMapSector(
            label,
            new TrackPoint(point.X - (nx * SectorHalfLength), point.Y - (ny * SectorHalfLength)),
            new TrackPoint(point.X + (nx * SectorHalfLength), point.Y + (ny * SectorHalfLength)),
            new TrackPoint(point.X + (nx * SectorLabelOffset), point.Y + (ny * SectorLabelOffset)));
    }
}
