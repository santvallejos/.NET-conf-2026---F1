using Microsoft.EntityFrameworkCore;

using PitWall.Data;
using PitWall.Domain;
using PitWall.Strategy.Degradation;

namespace PitWall.Web.Components.Panels.Tyres;

/// <summary>
/// Vueltas de una sesión ya clasificadas para la regresión (edad y compuesto por stint, in/out-lap, neutralizadas),
/// ordenadas por instante de cierre. Se leen de SQLite una vez por sesión.
/// </summary>
internal sealed class TyreSessionLaps
{
    private readonly LapSample[] _samples;
    private readonly DateTimeOffset[] _ends;
    private readonly IReadOnlyDictionary<int, TyreStint[]> _stints;

    private TyreSessionLaps(
        int sessionKey,
        int lapCount,
        List<(DateTimeOffset End, LapSample Sample)> completed,
        IReadOnlyDictionary<int, TyreStint[]> stints)
    {
        SessionKey = sessionKey;
        LapCount = lapCount;
        _samples = [.. completed.Select(lap => lap.Sample)];
        _ends = [.. completed.Select(lap => lap.End)];
        _stints = stints;
    }

    public int SessionKey { get; }

    public int LapCount { get; }

    public static TyreSessionLaps Empty(int sessionKey) => new(sessionKey, 0, [], new Dictionary<int, TyreStint[]>());

    /// <summary>Cantidad de vueltas (de toda la parrilla) cerradas en o antes de <paramref name="clock"/>.</summary>
    public int CompletedCount(DateTimeOffset clock)
    {
        var low = 0;
        var high = _ends.Length;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (_ends[middle] <= clock)
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

    /// <summary>Las primeras <paramref name="count"/> vueltas cerradas (ver <see cref="CompletedCount"/>).</summary>
    public IReadOnlyList<LapSample> Completed(int count) => new ArraySegment<LapSample>(_samples, 0, count);

    /// <summary>Stints del piloto iniciados hasta <paramref name="lap"/>, con el último cortado en esa vuelta.</summary>
    public IReadOnlyList<TyreStint> StintsUpTo(int driverNumber, int lap) =>
        _stints.TryGetValue(driverNumber, out var stints)
            ? [.. stints.Where(stint => stint.LapStart <= lap).Select(stint => stint with { LapEnd = Math.Min(stint.LapEnd, lap) })]
            : [];

    public static async Task<TyreSessionLaps> LoadAsync(PitWallDbContext db, int sessionKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        var laps = await db.Laps.AsNoTracking()
            .Where(e => e.SessionKey == sessionKey && e.DateStart != null)
            .ToListAsync(cancellationToken);
        var stints = await db.Stints.AsNoTracking().Where(e => e.SessionKey == sessionKey).ToListAsync(cancellationToken);
        var pitStops = await db.PitStops.AsNoTracking().Where(e => e.SessionKey == sessionKey).ToListAsync(cancellationToken);
        var raceControl = await db.RaceControl.AsNoTracking().Where(e => e.SessionKey == sessionKey).ToListAsync(cancellationToken);

        var neutralisation = new NeutralisationTimeline(raceControl.Select(e =>
            new RaceControlMessage(e.Date, e.LapNumber, e.Category, e.Flag, e.Scope, e.DriverNumber, e.Message)));
        var stintsByDriver = stints
            .GroupBy(e => e.DriverNumber)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(e => e.StintNumber)
                    .Select(e => new TyreStint(e.StintNumber, e.Compound, e.LapStart, e.LapEnd, e.TyreAgeAtStart))
                    .ToArray());
        var pitLaps = pitStops.Select(e => (e.DriverNumber, e.LapNumber)).ToHashSet();
        var outLaps = laps.Where(e => e.IsPitOutLap).Select(e => (e.DriverNumber, e.LapNumber)).ToHashSet();

        var completed = new List<(DateTimeOffset End, LapSample Sample)>(laps.Count);
        foreach (var lap in laps)
        {
            var stint = stintsByDriver.TryGetValue(lap.DriverNumber, out var driverStints)
                ? driverStints.FirstOrDefault(candidate => candidate.LapStart <= lap.LapNumber && lap.LapNumber <= candidate.LapEnd)
                : null;
            if (stint is null)
            {
                continue;
            }

            var start = lap.DateStart!.Value;
            var end = start + TimeSpan.FromSeconds(lap.Duration ?? 0);
            // Vuelta de entrada: la de la parada según OpenF1 o la anterior a una out-lap.
            var inLap = pitLaps.Contains((lap.DriverNumber, lap.LapNumber))
                || outLaps.Contains((lap.DriverNumber, lap.LapNumber + 1));
            completed.Add((end, new LapSample(
                lap.DriverNumber,
                lap.LapNumber,
                stint.Compound,
                stint.TyreAgeAtStart + (lap.LapNumber - stint.LapStart),
                lap.Duration,
                lap.IsPitOutLap,
                inLap,
                neutralisation.IsNeutralised(start, end))));
        }

        completed.Sort((left, right) => left.End.CompareTo(right.End));
        return new TyreSessionLaps(sessionKey, laps.Count == 0 ? 0 : laps.Max(e => e.LapNumber), completed, stintsByDriver);
    }
}
