using Microsoft.EntityFrameworkCore;

using PitWall.Data;
using PitWall.Domain;
using PitWall.Strategy.Degradation;
using PitWall.Strategy.Timing;

namespace PitWall.Web.Components.Panels.Laps;

/// <summary>Contexto de una vuelta para el ritmo de carrera: compuesto del stint y si estuvo neutralizada.</summary>
public sealed record LapContext(Compound? Compound, bool Neutralised);

/// <summary>Lee de SQLite las vueltas de una sesión y las mapea al modelo de timing.</summary>
internal static class LapTimingSessionLaps
{
    public static async Task<(List<TimingLap> Laps, Dictionary<(int DriverNumber, int LapNumber), LapContext> Contexts)> LoadAsync(
        PitWallDbContext db,
        int sessionKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        var laps = await db.Laps.AsNoTracking()
            .Where(e => e.SessionKey == sessionKey && e.DateStart != null)
            .ToListAsync(cancellationToken);
        var pitStops = await db.PitStops.AsNoTracking().Where(e => e.SessionKey == sessionKey).ToListAsync(cancellationToken);
        var stints = await db.Stints.AsNoTracking().Where(e => e.SessionKey == sessionKey).ToListAsync(cancellationToken);
        var raceControl = await db.RaceControl.AsNoTracking().Where(e => e.SessionKey == sessionKey).ToListAsync(cancellationToken);

        // Misma interpretación de SC/VSC/amarilla/roja que la degradación de neumáticos.
        var neutralisation = new NeutralisationTimeline(raceControl.Select(e =>
            new RaceControlMessage(e.Date, e.LapNumber, e.Category, e.Flag, e.Scope, e.DriverNumber, e.Message)));
        var stintsByDriver = stints.GroupBy(e => e.DriverNumber).ToDictionary(group => group.Key, group => group.ToArray());

        var pitLaps = pitStops.Select(e => (e.DriverNumber, e.LapNumber)).ToHashSet();
        var outLaps = laps.Where(e => e.IsPitOutLap).Select(e => (e.DriverNumber, e.LapNumber)).ToHashSet();
        var contexts = new Dictionary<(int DriverNumber, int LapNumber), LapContext>(laps.Count);
        foreach (var lap in laps)
        {
            var start = lap.DateStart!.Value;
            var stint = stintsByDriver.GetValueOrDefault(lap.DriverNumber)?
                .FirstOrDefault(candidate => candidate.LapStart <= lap.LapNumber && lap.LapNumber <= candidate.LapEnd);
            contexts[(lap.DriverNumber, lap.LapNumber)] = new LapContext(
                stint?.Compound,
                lap.Duration is { } duration && neutralisation.IsNeutralised(start, start + TimeSpan.FromSeconds(duration)));
        }

        List<TimingLap> timingLaps = [.. laps.Select(lap => new TimingLap(
            lap.DriverNumber,
            lap.LapNumber,
            lap.DateStart!.Value,
            lap.Duration,
            lap.S1,
            lap.S2,
            lap.S3,
            lap.IsPitOutLap,
            // Vuelta de entrada: la de la parada según OpenF1 o la anterior a una out-lap.
            pitLaps.Contains((lap.DriverNumber, lap.LapNumber)) || outLaps.Contains((lap.DriverNumber, lap.LapNumber + 1))))];
        return (timingLaps, contexts);
    }
}
