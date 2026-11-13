using PitWall.Data.Entities;
using PitWall.Domain;

namespace PitWall.Replay;

/// <summary>Datos cacheados de un piloto para reconstruir su <see cref="CarState"/> en cualquier instante.</summary>
internal sealed class DriverTimeline
{
    private readonly Timeline<int> _lapStarts;
    private readonly IReadOnlyList<Stint> _stints;
    private readonly IReadOnlyList<(DateTimeOffset Entry, DateTimeOffset Exit)> _pitWindows;
    private readonly Timeline<int> _positions;
    private readonly Timeline<(double? GapToLeader, double? Interval)> _intervals;
    private readonly int? _gridPosition;

    public DriverTimeline(
        int driverNumber,
        int? gridPosition,
        IEnumerable<Lap> laps,
        IEnumerable<Stint> stints,
        IEnumerable<PitStop> pitStops,
        IEnumerable<PositionSample> positions,
        IEnumerable<IntervalSample> intervals)
    {
        DriverNumber = driverNumber;
        _gridPosition = gridPosition;
        _lapStarts = new Timeline<int>(laps
            .Where(lap => lap.DateStart is not null)
            .OrderBy(lap => lap.LapNumber)
            .Select(lap => (lap.DateStart!.Value, lap.LapNumber)));
        _stints = [.. stints.OrderBy(stint => stint.StintNumber)];
        _pitWindows = [.. pitStops.Select(pit => (pit.Date, pit.Date + TimeSpan.FromSeconds(pit.LaneDuration ?? pit.PitDuration ?? 0)))];
        _positions = new Timeline<int>(positions.Select(sample => (sample.Date, sample.Position)));
        _intervals = new Timeline<(double?, double?)>(intervals.Select(sample => (sample.Date, (sample.GapToLeader, sample.Interval))));
    }

    public int DriverNumber { get; }

    public bool HasStints => _stints.Count > 0;

    /// <summary>Vuelta en curso: la última cuyo inicio ya ocurrió; 0 antes de la largada.</summary>
    public int LapAt(DateTimeOffset clock) => _lapStarts.TryGetAtOrBefore(clock, out var lap) ? lap : 0;

    public (Compound Compound, int TyreAge) TyreAt(int lap)
    {
        var stint = _stints.LastOrDefault(candidate => candidate.LapStart <= lap) ?? _stints[0];
        return (stint.Compound, stint.TyreAgeAtStart + Math.Max(0, lap - stint.LapStart));
    }

    public int? PositionAt(DateTimeOffset clock) => _positions.TryGetAtOrBefore(clock, out var position) ? position : _gridPosition;

    public (double? GapToLeader, double? Interval) GapsAt(DateTimeOffset clock) =>
        _intervals.TryGetAtOrBefore(clock, out var gaps) ? gaps : (null, null);

    public bool InPitAt(DateTimeOffset clock) => _pitWindows.Any(window => window.Entry <= clock && clock < window.Exit);
}
