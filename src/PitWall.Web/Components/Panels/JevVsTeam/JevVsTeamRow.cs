using PitWall.Domain;
using PitWall.Strategy.TeamDecision;

namespace PitWall.Web.Components.Panels.JevVsTeam;

/// <summary>Una vuelta del panel Jev vs equipo.</summary>
/// <param name="Jev">Última decisión de estrategia de Jev en la vuelta; null si no fue consultado.</param>
/// <param name="Team">Decisión real del equipo en la vuelta; null si no hay datos de esa vuelta.</param>
/// <param name="TeamStop">Parada real con la que se comparó la llamada de Jev (la de la vuelta o la más cercana).</param>
/// <param name="OffsetLaps">Vueltas de adelanto o atraso de Jev; 0 si no aplica.</param>
/// <param name="CompoundDiffers">Jev y la parada real comparada eligieron compuestos distintos.</param>
public sealed record JevVsTeamRow(
    int Lap,
    JevDecision? Jev,
    TeamLapDecision? Team,
    TeamLapDecision? TeamStop,
    JevVsTeamOutcome Outcome,
    int OffsetLaps,
    bool CompoundDiffers);
