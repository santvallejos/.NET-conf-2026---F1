using PitWall.Domain;
using PitWall.Strategy.TeamDecision;

namespace PitWall.Web.Components.Panels.JevVsTeam;

/// <summary>
/// Compara por vuelta la llamada de pit + compuesto de Jev con la decisión real del equipo (pace y energía reales no
/// son observables). Ambas usan la vuelta del piloto.
/// </summary>
public static class JevVsTeamComparer
{
    /// <summary>Distancia máxima, en vueltas, para considerar que una parada de Jev es la misma que una real.</summary>
    public const int MaxOffsetLaps = 5;

    /// <param name="team">Decisión real del piloto por vuelta (<see cref="TeamDecisionDeriver"/>).</param>
    /// <param name="decisions">Decisiones de Jev de la sesión; se usan solo las de estrategia reales del piloto.</param>
    /// <param name="currentLap">Vuelta actual del piloto: las paradas reales sin Jev se listan hasta ella.</param>
    /// <returns>Filas de la vuelta más nueva a la más vieja.</returns>
    public static IReadOnlyList<JevVsTeamRow> Compare(
        IReadOnlyList<TeamLapDecision> team,
        IEnumerable<JevDecision> decisions,
        int driverNumber,
        int currentLap)
    {
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(decisions);

        // La última decisión de cada vuelta manda (p. ej. un "Consultar Jev" después de la automática).
        var jevByLap = new Dictionary<int, JevDecision>();
        foreach (var decision in decisions)
        {
            if (decision.DriverNumber == driverNumber
                && decision.Kind == JevKind.Strategy
                && !decision.Hypothetical
                && decision.PitCall is not null)
            {
                jevByLap[decision.Lap] = decision;
            }
        }

        var teamByLap = team.ToDictionary(decision => decision.Lap);
        var stops = team.Where(decision => decision.PitCall == PitCall.Box).ToList();

        var rows = jevByLap.Values
            .Select(decision => Row(decision, teamByLap.GetValueOrDefault(decision.Lap), stops, team.Count == 0))
            .Concat(stops
                .Where(stop => stop.Lap <= currentLap && !jevByLap.ContainsKey(stop.Lap))
                .Select(stop => new JevVsTeamRow(stop.Lap, null, stop, stop, JevVsTeamOutcome.NoJev, 0, false)));

        return [.. rows.OrderByDescending(row => row.Lap)];
    }

    private static JevVsTeamRow Row(JevDecision decision, TeamLapDecision? team, List<TeamLapDecision> stops, bool noTeam)
    {
        if (noTeam)
        {
            return new JevVsTeamRow(decision.Lap, decision, null, null, JevVsTeamOutcome.NoTeam, 0, false);
        }

        // BOX = parar al final de esta vuelta; BOX_NEXT_LAP = al final de la siguiente.
        int? jevStopLap = decision.PitCall switch
        {
            PitCall.Box => decision.Lap,
            PitCall.BoxNextLap => decision.Lap + 1,
            _ => null,
        };

        if (jevStopLap is not { } target)
        {
            return team?.PitCall == PitCall.Box
                ? new JevVsTeamRow(decision.Lap, decision, team, team, JevVsTeamOutcome.Differs, 0, false)
                : new JevVsTeamRow(decision.Lap, decision, team, null, JevVsTeamOutcome.Match, 0, false);
        }

        var nearest = stops
            .Where(stop => Math.Abs(stop.Lap - target) <= MaxOffsetLaps)
            .OrderBy(stop => Math.Abs(stop.Lap - target))
            .ThenBy(stop => stop.Lap)
            .FirstOrDefault();
        if (nearest is null)
        {
            return new JevVsTeamRow(decision.Lap, decision, team, null, JevVsTeamOutcome.Differs, 0, false);
        }

        // Sin compuesto conocido de un lado no hay diferencia que marcar.
        var compoundDiffers = decision.Compound is { } jevCompound
            && nearest.Compound is { } teamCompound
            && jevCompound != teamCompound;
        var offset = nearest.Lap - target;
        var outcome = offset switch
        {
            > 0 => JevVsTeamOutcome.JevAhead,
            < 0 => JevVsTeamOutcome.JevBehind,
            _ => compoundDiffers ? JevVsTeamOutcome.Differs : JevVsTeamOutcome.Match,
        };

        return new JevVsTeamRow(decision.Lap, decision, team, nearest, outcome, Math.Abs(offset), compoundDiffers);
    }
}
