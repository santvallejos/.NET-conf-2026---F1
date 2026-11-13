using PitWall.Strategy.Rules;
using PitWall.Strategy.Scenarios;

namespace PitWall.Web.Services;

/// <summary>Textos en castellano de los eventos inyectables para la UI.</summary>
public static class ScenarioLabels
{
    public static string Of(InjectedEventType type) => type switch
    {
        InjectedEventType.SafetyCar => "Safety car",
        InjectedEventType.VirtualSafetyCar => "VSC",
        InjectedEventType.RedFlag => "Bandera roja",
        InjectedEventType.Rain => "Lluvia",
        InjectedEventType.Puncture => "Pinchazo",
        InjectedEventType.WingDamage => "Daño de alerón",
        InjectedEventType.RivalCrash => "Choque de rival",
        InjectedEventType.ErsFailure => "Falla de ERS",
        _ => type.ToString(),
    };

    /// <summary>Nombre del escenario que se crea al inyectar sobre <c>Real</c>, p. ej. "Safety car V40".</summary>
    public static string DefaultScenarioName(InjectedEventType type, int lap) => $"{Of(type)} V{lap}";

    /// <summary>Parámetros del evento, p. ej. "4 vueltas", "fuerte · 5 vueltas", "#12 adelante de #43".</summary>
    public static string Parameters(InjectedEvent injected)
    {
        ArgumentNullException.ThrowIfNull(injected);

        var parameters = injected.Parameters;
        var parts = new List<string>();
        if (parameters.Intensity is { } intensity)
        {
            parts.Add(intensity == RainIntensity.Heavy ? "fuerte" : "leve");
        }

        if (parameters.Severity is { } severity)
        {
            parts.Add(severity == DamageSeverity.Major ? "grave" : "leve");
        }

        if (parameters.Rival is { } rival)
        {
            var relative = parameters.Relative == RivalRelative.Behind ? "atrás" : "adelante";
            parts.Add(parameters.Driver is { } reference ? $"#{rival} {relative} de #{reference}" : $"#{rival}");
        }
        else if (parameters.Driver is { } driver)
        {
            parts.Add($"#{driver}");
        }

        if (parameters.DurationLaps is { } duration)
        {
            parts.Add(duration == 1 ? "1 vuelta" : $"{duration} vueltas");
        }

        return string.Join(" · ", parts);
    }
}
