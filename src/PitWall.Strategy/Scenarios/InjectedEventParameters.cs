using PitWall.Strategy.Rules;

namespace PitWall.Strategy.Scenarios;

/// <summary>
/// Parámetros de un evento inyectado (docs/scenarios.md §3). Cada tipo usa solo los suyos; el resto queda null.
/// </summary>
/// <remarks>Se persisten como JSON en <c>InjectedEvent.Params</c>.</remarks>
public sealed record InjectedEventParameters
{
    /// <summary>Vueltas que dura el SC, el VSC o la lluvia.</summary>
    public int? DurationLaps { get; init; }

    /// <summary>Intensidad de la lluvia.</summary>
    public RainIntensity? Intensity { get; init; }

    /// <summary>Gravedad del daño de alerón o de la falla de ERS.</summary>
    public DamageSeverity? Severity { get; init; }

    /// <summary>Auto afectado (pinchazo, alerón, ERS) o de referencia del choque de un rival.</summary>
    public int? Driver { get; init; }

    /// <summary>Rival que choca.</summary>
    public int? Rival { get; init; }

    /// <summary>Si el rival que choca va adelante o atrás de <see cref="Driver"/>.</summary>
    public RivalRelative? Relative { get; init; }
}
