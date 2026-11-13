using PitWall.Domain;

namespace PitWall.Strategy.Rules;

/// <summary>
/// Motor de reglas: restricciones duras que filtran candidatas y decisiones de Jev, y razones del enum
/// cerrado <see cref="Reason"/> calculadas en C# porque Jev no las devuelve (ADR-009).
/// </summary>
public sealed class RulesEngine
{
    private static readonly Compound[] DryCompounds = [Compound.Soft, Compound.Medium, Compound.Hard];

    private readonly RulesOptions _options;

    public RulesEngine(RulesOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public IReadOnlyList<Constraint> EvaluateConstraints(RuleFeatures features)
    {
        ArgumentNullException.ThrowIfNull(features);

        var constraints = new List<Constraint>();

        if (IsRedFlag(features.Track))
        {
            constraints.Add(Forbid(ConstraintRules.RedFlag, PitCall.Box));
        }

        if (features.PitLaneClosed)
        {
            constraints.Add(Forbid(ConstraintRules.PitLaneClosed, PitCall.Box));
        }

        if (IsMandatoryCompoundPending(features))
        {
            constraints.Add(new Constraint(ConstraintRules.MandatoryDryCompounds, [], [], null, true));
        }

        if (features.Rain == RainIntensity.Heavy)
        {
            constraints.Add(new Constraint(ConstraintRules.HeavyRain, [], DryCompounds, null, false));
        }

        if (!features.TrackWet && features.Rain == RainIntensity.None && !features.RainForecast)
        {
            constraints.Add(new Constraint(ConstraintRules.DryTrack, [], [Compound.Wet], null, false));
        }

        if (features.Puncture)
        {
            constraints.Add(Force(ConstraintRules.Puncture));
        }

        if (features.WingDamage == DamageSeverity.Major)
        {
            constraints.Add(Force(ConstraintRules.WingDamage));
        }

        return constraints;
    }

    public IReadOnlyList<Reason> EvaluateReasons(RuleFeatures features)
    {
        ArgumentNullException.ThrowIfNull(features);

        var reasons = new List<Reason>();
        var tyre = features.Tyre;
        var raining = features.Rain != RainIntensity.None;
        var wasRaining = features.PreviousRain != RainIntensity.None;

        Add(tyre.DegPerLap >= _options.DegHighSecondsPerLap, Reason.TyreDegHigh);
        Add(tyre.LapsToCliff <= _options.CliffNearLaps, Reason.TyreCliffNear);
        Add(features.GapBehind <= _options.UndercutGapSeconds, Reason.UndercutThreat);
        Add(features.RivalAheadPitted && tyre.LapsToCliff > _options.CliffNearLaps, Reason.OvercutWindow);
        Add(features.Track.SafetyCar, Reason.ScDeployed);
        Add(features.Track.VirtualSafetyCar, Reason.VscDeployed);
        Add(IsRedFlag(features.Track), Reason.RedFlag);
        Add(raining && !wasRaining, Reason.RainStarting);
        Add(!raining && wasRaining, Reason.RainStopping);
        Add(!raining && !wasRaining && features.TrackWet, Reason.TrackDrying);
        Add(IsCompoundMismatched(tyre.Compound, raining || features.TrackWet), Reason.LowGrip);
        Add(features.ExitGap < _options.TrafficOnExitGapSeconds, Reason.TrafficOnExit);
        Add(features.ExitGap >= _options.TrafficOnExitGapSeconds, Reason.ClearAirOnExit);
        Add(features.Energy.SocPct <= _options.EnergyLowSocPct, Reason.EnergyLow);
        Add(features.Energy.OvertakeAvailable && !features.ErsFailure, Reason.OvertakeAvailable);
        Add(features.Puncture, Reason.Puncture);
        Add(features.WingDamage != DamageSeverity.None, Reason.WingDamage);
        Add(features.RivalIncident, Reason.RivalIncident);
        Add(features.ErsFailure, Reason.ErsFailure);
        Add(IsMandatoryCompoundPending(features), Reason.MandatoryCompoundPending);

        return reasons;

        void Add(bool condition, Reason reason)
        {
            if (condition)
            {
                reasons.Add(reason);
            }
        }
    }

    /// <summary>True si la pit call (y el compuesto a montar, si hay) no viola ninguna restricción.</summary>
    public static bool IsAllowed(IReadOnlyList<Constraint> constraints, PitCall call, Compound? compound)
    {
        ArgumentNullException.ThrowIfNull(constraints);

        if (constraints.Any(c => c.ForbiddenCalls.Contains(call)))
        {
            return false;
        }

        if (compound is { } fitted && constraints.Any(c => c.ForbiddenCompounds.Contains(fitted)))
        {
            return false;
        }

        // Una pit call forzada que otra regla prohíbe (p. ej. pinchazo con bandera roja: el cambio se hace
        // en parrilla) no puede cumplirse, así que no restringe al resto.
        var forced = constraints
            .Select(c => c.ForcedCall)
            .OfType<PitCall>()
            .Where(f => !constraints.Any(c => c.ForbiddenCalls.Contains(f)))
            .ToList();

        return forced.Count == 0 || forced.Contains(call);
    }

    private static bool IsRedFlag(TrackStatus track) =>
        string.Equals(track.Flag, "RED", StringComparison.OrdinalIgnoreCase);

    // Carrera en seco = ningún compuesto húmedo usado; entonces se exigen al menos 2 compuestos secos distintos.
    private static bool IsMandatoryCompoundPending(RuleFeatures features)
    {
        var used = features.UsedCompounds.Append(features.Tyre.Compound).ToHashSet();
        return used.All(c => DryCompounds.Contains(c)) && used.Count < 2;
    }

    // Goma seca con pista mojada o lluvia, o goma de lluvia con pista seca.
    private static bool IsCompoundMismatched(Compound compound, bool wetConditions) =>
        DryCompounds.Contains(compound) == wetConditions;

    private static Constraint Forbid(string rule, PitCall call) => new(rule, [call], [], null, false);

    private static Constraint Force(string rule) => new(rule, [], [], PitCall.Box, false);
}
