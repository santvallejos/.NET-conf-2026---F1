namespace PitWall.Strategy.Timing;

/// <summary>Clasificación de un tiempo de sector o de vuelta al momento de marcarlo.</summary>
public enum TimingClass
{
    /// <summary>Mejor de la sesión (púrpura).</summary>
    SessionBest,

    /// <summary>Mejor personal (verde).</summary>
    PersonalBest,

    /// <summary>Sin mejora (amarillo).</summary>
    NoImprovement,
}
