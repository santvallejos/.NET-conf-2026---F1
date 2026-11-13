namespace PitWall.Web.Components.Panels.JevVsTeam;

/// <summary>Resultado de comparar la llamada de Jev con la decisión real del equipo en una vuelta.</summary>
public enum JevVsTeamOutcome
{
    /// <summary>Misma vuelta de parada (o ambos siguen afuera) y mismo compuesto.</summary>
    Match,

    /// <summary>Distinta llamada sin parada real cercana, o misma vuelta con otro compuesto.</summary>
    Differs,

    /// <summary>Jev para N vueltas antes que el equipo.</summary>
    JevAhead,

    /// <summary>Jev para N vueltas después que el equipo.</summary>
    JevBehind,

    /// <summary>El equipo paró en una vuelta en la que Jev no fue consultado.</summary>
    NoJev,

    /// <summary>La sesión no tiene stints ni paradas del piloto.</summary>
    NoTeam,
}
