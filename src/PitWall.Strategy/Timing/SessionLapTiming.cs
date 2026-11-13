namespace PitWall.Strategy.Timing;

/// <summary>
/// Timing por vuelta de una sesión. Cada vuelta se clasifica una sola vez contra los mejores vigentes al cerrarse
/// (como en el timing oficial); <see cref="At"/> solo considera las vueltas completas al instante pedido.
/// </summary>
public sealed class SessionLapTiming
{
    private const int S1 = 0;
    private const int S2 = 1;
    private const int S3 = 2;
    private const int Lap = 3;

    private readonly TimedLap[] _laps;

    public SessionLapTiming(IEnumerable<TimingLap> laps)
    {
        ArgumentNullException.ThrowIfNull(laps);

        var ordered = laps
            .Where(lap => lap.Duration is not null)
            .Select(lap => (Lap: lap, End: lap.DateStart + TimeSpan.FromSeconds(lap.Duration!.Value)))
            .OrderBy(lap => lap.End)
            .ThenBy(lap => lap.Lap.DriverNumber)
            .ThenBy(lap => lap.Lap.LapNumber)
            .ToList();

        var sessionBests = new double?[4];
        var personalBests = new Dictionary<int, double?[]>();
        _laps = new TimedLap[ordered.Count];
        for (var index = 0; index < ordered.Count; index++)
        {
            var (lap, end) = ordered[index];
            if (!personalBests.TryGetValue(lap.DriverNumber, out var personal))
            {
                personal = new double?[4];
                personalBests[lap.DriverNumber] = personal;
            }

            _laps[index] = new TimedLap(
                lap.DriverNumber,
                lap.LapNumber,
                end,
                lap.IsPitOutLap,
                lap.IsInLap,
                Classify(lap.S1, ref sessionBests[S1], ref personal[S1]),
                Classify(lap.S2, ref sessionBests[S2], ref personal[S2]),
                Classify(lap.S3, ref sessionBests[S3], ref personal[S3]),
                Classify(lap.Duration, ref sessionBests[Lap], ref personal[Lap])!);
        }
    }

    /// <summary>Vueltas con duración (las que en algún instante quedan completas).</summary>
    public int LapCount => _laps.Length;

    /// <summary>Cantidad de vueltas (de toda la parrilla) completas en o antes de <paramref name="clock"/>.</summary>
    public int CompletedCount(DateTimeOffset clock)
    {
        var low = 0;
        var high = _laps.Length;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (_laps[middle].End <= clock)
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

    public LapTimingSnapshot At(DateTimeOffset clock)
    {
        var completed = CompletedCount(clock);
        var drivers = new Dictionary<int, DriverTiming>();
        foreach (var group in _laps.Take(completed).GroupBy(lap => lap.DriverNumber))
        {
            var laps = group.OrderBy(lap => lap.LapNumber).ToArray();
            var bestS1 = Best(laps, lap => lap.S1);
            var bestS2 = Best(laps, lap => lap.S2);
            var bestS3 = Best(laps, lap => lap.S3);
            // Redondeo a milésimas (la resolución de OpenF1) para que la suma no arrastre error de punto flotante.
            double? ideal = bestS1 is { } s1 && bestS2 is { } s2 && bestS3 is { } s3 ? Math.Round(s1 + s2 + s3, 3) : null;
            drivers[group.Key] = new DriverTiming(
                group.Key,
                laps[^1],
                laps,
                bestS1,
                bestS2,
                bestS3,
                laps.Min(lap => lap.Lap.Seconds),
                ideal);
        }

        var rankings = new TimingRankings(
            Rank(drivers.Values, driver => driver.BestS1),
            Rank(drivers.Values, driver => driver.BestS2),
            Rank(drivers.Values, driver => driver.BestS3),
            Rank(drivers.Values, driver => driver.BestLap),
            Rank(drivers.Values, driver => driver.IdealLap));
        return new LapTimingSnapshot(completed, drivers, rankings);
    }

    // Igualar el mejor vigente también cuenta como mejor.
    private static TimedValue? Classify(double? seconds, ref double? sessionBest, ref double? personalBest)
    {
        if (seconds is not { } value)
        {
            return null;
        }

        var timingClass = sessionBest is null || value <= sessionBest
            ? TimingClass.SessionBest
            : personalBest is null || value <= personalBest
                ? TimingClass.PersonalBest
                : TimingClass.NoImprovement;
        sessionBest = sessionBest is { } session ? Math.Min(session, value) : value;
        personalBest = personalBest is { } personal ? Math.Min(personal, value) : value;
        return new TimedValue(value, timingClass);
    }

    private static double? Best(IEnumerable<TimedLap> laps, Func<TimedLap, TimedValue?> sector) =>
        laps.Select(sector).Where(value => value is not null).Min(value => (double?)value!.Seconds);

    private static TimingRankEntry[] Rank(IEnumerable<DriverTiming> drivers, Func<DriverTiming, double?> value) =>
        [.. drivers
            .Where(driver => value(driver) is not null)
            .Select(driver => new TimingRankEntry(driver.DriverNumber, value(driver)!.Value))
            .OrderBy(entry => entry.Seconds)
            .ThenBy(entry => entry.DriverNumber)];
}
