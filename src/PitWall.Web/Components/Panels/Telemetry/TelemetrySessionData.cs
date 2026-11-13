using Microsoft.EntityFrameworkCore;

using PitWall.Data;

namespace PitWall.Web.Components.Panels.Telemetry;

/// <summary>
/// <c>car_data</c> de los pilotos ingeridos y el inicio y la duración de cada vuelta, en orden cronológico. Se lee de
/// SQLite una vez por sesión; el foco se cambia sin volver a leer.
/// </summary>
internal sealed class TelemetrySessionData
{
    private readonly IReadOnlyDictionary<int, TelemetrySample[]> _samples;
    private readonly IReadOnlyDictionary<int, DateTimeOffset[]> _dates;
    private readonly IReadOnlyDictionary<int, (int Lap, DateTimeOffset Start, double? Duration)[]> _laps;

    private TelemetrySessionData(
        int sessionKey,
        IReadOnlyDictionary<int, TelemetrySample[]> samples,
        IReadOnlyDictionary<int, (int Lap, DateTimeOffset Start, double? Duration)[]> laps)
    {
        SessionKey = sessionKey;
        _samples = samples;
        _dates = samples.ToDictionary(pair => pair.Key, pair => pair.Value.Select(sample => sample.Date).ToArray());
        _laps = laps;
    }

    public int SessionKey { get; }

    public static TelemetrySessionData Empty(int sessionKey) =>
        new(sessionKey, new Dictionary<int, TelemetrySample[]>(), new Dictionary<int, (int, DateTimeOffset, double?)[]>());

    public bool HasTelemetry(int driverNumber) => _samples.TryGetValue(driverNumber, out var samples) && samples.Length > 0;

    /// <summary>Vueltas del piloto con inicio conocido, en orden.</summary>
    public IReadOnlyList<(int Lap, DateTimeOffset Start, double? Duration)> Laps(int driverNumber) =>
        _laps.TryGetValue(driverNumber, out var laps) ? laps : [];

    /// <summary>Última muestra con fecha ≤ <paramref name="clock"/>, o null.</summary>
    public TelemetrySample? At(int driverNumber, DateTimeOffset clock)
    {
        if (!_samples.TryGetValue(driverNumber, out var samples))
        {
            return null;
        }

        var count = CountUpTo(_dates[driverNumber], clock);
        return count == 0 ? null : samples[count - 1];
    }

    /// <summary>Muestras con fecha en [<paramref name="from"/>, <paramref name="to"/>].</summary>
    public ArraySegment<TelemetrySample> Between(int driverNumber, DateTimeOffset from, DateTimeOffset to)
    {
        if (!_samples.TryGetValue(driverNumber, out var samples) || to < from)
        {
            return ArraySegment<TelemetrySample>.Empty;
        }

        var dates = _dates[driverNumber];
        var start = CountUpTo(dates, from.AddTicks(-1));
        var end = CountUpTo(dates, to);
        return new ArraySegment<TelemetrySample>(samples, start, end - start);
    }

    public static async Task<TelemetrySessionData> LoadAsync(PitWallDbContext db, int sessionKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        var samples = await db.CarData.AsNoTracking()
            .Where(e => e.SessionKey == sessionKey)
            .Select(e => new { e.DriverNumber, Sample = new TelemetrySample(e.Date, e.Speed, e.Throttle, e.Brake, e.Gear, e.Rpm) })
            .ToListAsync(cancellationToken);
        var laps = await db.Laps.AsNoTracking()
            .Where(e => e.SessionKey == sessionKey && e.DateStart != null)
            .Select(e => new { e.DriverNumber, e.LapNumber, e.DateStart, e.Duration })
            .ToListAsync(cancellationToken);

        return new TelemetrySessionData(
            sessionKey,
            samples
                .GroupBy(e => e.DriverNumber)
                .ToDictionary(group => group.Key, group => group.Select(e => e.Sample).OrderBy(sample => sample.Date).ToArray()),
            laps
                .GroupBy(e => e.DriverNumber)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(e => (Lap: e.LapNumber, Start: e.DateStart!.Value, e.Duration)).OrderBy(lap => lap.Start).ToArray()));
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
}
