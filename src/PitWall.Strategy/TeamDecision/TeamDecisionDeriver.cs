using PitWall.Domain;

namespace PitWall.Strategy.TeamDecision;

/// <summary>
/// Deriva la decisión real del equipo por vuelta desde <c>stints</c> + <c>pit</c>: <see cref="PitCall.Box"/> en la
/// vuelta de cada parada con el compuesto del stint siguiente, <see cref="PitCall.StayOut"/> en el resto.
/// </summary>
public static class TeamDecisionDeriver
{
    /// <returns>Una decisión por vuelta, ordenadas, desde la primera vuelta de stint hasta la última vuelta conocida.</returns>
    public static IReadOnlyList<TeamLapDecision> Derive(
        int driverNumber,
        IEnumerable<TeamStint> stints,
        IEnumerable<TeamPitStop> pitStops)
    {
        ArgumentNullException.ThrowIfNull(stints);
        ArgumentNullException.ThrowIfNull(pitStops);

        var driverStints = stints
            .Where(stint => stint.DriverNumber == driverNumber)
            .OrderBy(stint => stint.LapStart)
            .ToList();
        var pitLaps = pitStops
            .Where(pit => pit.DriverNumber == driverNumber)
            .Select(pit => pit.LapNumber)
            .ToHashSet();

        if (driverStints.Count == 0 && pitLaps.Count == 0)
        {
            return [];
        }

        var firstLap = driverStints.Select(stint => stint.LapStart).Concat(pitLaps).Min();
        var lastLap = driverStints.Select(stint => stint.LapEnd).Concat(pitLaps).Max();

        var decisions = new List<TeamLapDecision>(lastLap - firstLap + 1);
        for (var lap = firstLap; lap <= lastLap; lap++)
        {
            decisions.Add(pitLaps.Contains(lap)
                ? new TeamLapDecision(driverNumber, lap, PitCall.Box, NextStintCompound(driverStints, lap))
                : new TeamLapDecision(driverNumber, lap, PitCall.StayOut, null));
        }

        return decisions;
    }

    private static Compound? NextStintCompound(List<TeamStint> orderedStints, int pitLap) =>
        orderedStints.FirstOrDefault(stint => stint.LapStart > pitLap)?.Compound;
}
