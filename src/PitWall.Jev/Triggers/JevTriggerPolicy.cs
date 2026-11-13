using PitWall.Domain;

namespace PitWall.Jev.Triggers;

/// <summary>Cuándo consultar a Jev según <c>Jev:Enabled</c>, <c>Jev:Drivers</c> y <c>Jev:Mode</c> (docs/jev.md §7).</summary>
public static class JevTriggerPolicy
{
    /// <summary>Jev activo globalmente y para el piloto.</summary>
    public static bool IsDriverEnabled(JevOptions options, int driverNumber)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Enabled && options.Drivers.Contains(driverNumber);
    }

    /// <summary>Disparo de estrategia al cerrar <paramref name="lap"/>, o null si el modo no lo pide.</summary>
    public static JevTrigger? OnLapCompleted(JevOptions options, int driverNumber, int lap)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!IsDriverEnabled(options, driverNumber))
        {
            return null;
        }

        return options.Mode switch
        {
            JevMode.EveryLap => JevTrigger.EveryLap,
            JevMode.SpecificLaps when options.Laps.Contains(lap) => JevTrigger.SpecificLap,
            _ => null,
        };
    }

    /// <summary>Triage en todos los modos si <c>Jev:Triage:Enabled</c> (docs/jev.md §7).</summary>
    public static bool ShouldTriage(JevOptions options, int driverNumber)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Triage.Enabled && IsDriverEnabled(options, driverNumber);
    }

    /// <summary>Un triage <c>ACT_NOW</c> dispara la estrategia aunque el modo sea <see cref="JevMode.Manual"/>.</summary>
    public static bool TriageTriggersStrategy(JevOptions options, JevDecision triage)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(triage);

        return triage.Kind == JevKind.Triage
            && triage.Impact == Impact.ActNow
            && options.Triage.CanTriggerStrategy
            && IsDriverEnabled(options, triage.DriverNumber);
    }
}
