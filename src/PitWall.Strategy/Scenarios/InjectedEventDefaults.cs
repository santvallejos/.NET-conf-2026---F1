using PitWall.Domain;
using PitWall.Strategy.Rules;

namespace PitWall.Strategy.Scenarios;

/// <summary>Completa los parámetros de un evento con los defaults de <c>Scenarios:*</c> y descarta los que no usa.</summary>
public static class InjectedEventDefaults
{
    /// <param name="focusDriver">Auto propio por defecto (pinchazo, alerón, ERS, referencia del choque).</param>
    /// <exception cref="ArgumentException">Choque de rival sin <see cref="InjectedEventParameters.Rival"/>.</exception>
    public static InjectedEventParameters Apply(InjectedEventType type, InjectedEventParameters parameters, ScenarioOptions options, int focusDriver)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(options);
        if (parameters.DurationLaps is { } duration)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(duration, 1, nameof(parameters));
        }

        var driver = parameters.Driver ?? focusDriver;
        return type switch
        {
            InjectedEventType.SafetyCar => new() { DurationLaps = parameters.DurationLaps ?? options.SafetyCarDefaultLaps },
            InjectedEventType.VirtualSafetyCar => new() { DurationLaps = parameters.DurationLaps ?? options.VscDefaultLaps },
            InjectedEventType.RedFlag => new(),
            InjectedEventType.Rain => new()
            {
                Intensity = parameters.Intensity is RainIntensity.Heavy ? RainIntensity.Heavy : RainIntensity.Light,
                DurationLaps = parameters.DurationLaps ?? options.RainDefaultLaps,
            },
            InjectedEventType.Puncture => new() { Driver = driver },
            InjectedEventType.WingDamage or InjectedEventType.ErsFailure => new()
            {
                Severity = parameters.Severity is DamageSeverity.Major ? DamageSeverity.Major : DamageSeverity.Minor,
                Driver = driver,
            },
            InjectedEventType.RivalCrash => new()
            {
                Driver = driver,
                Rival = parameters.Rival ?? throw new ArgumentException("A rival crash needs the rival driver.", nameof(parameters)),
                Relative = parameters.Relative ?? RivalRelative.Ahead,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown injected event type."),
        };
    }

    /// <summary>Auto inmediatamente adelante o atrás de <paramref name="driver"/> en <paramref name="state"/>, o null.</summary>
    public static int? RivalOf(RaceState state, int driver, RivalRelative relative)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Cars.FirstOrDefault(car => car.DriverNumber == driver) is not { Position: > 0 } reference)
        {
            return null;
        }

        var position = relative == RivalRelative.Ahead ? reference.Position - 1 : reference.Position + 1;
        return state.Cars.FirstOrDefault(car => car.Position == position)?.DriverNumber;
    }
}
