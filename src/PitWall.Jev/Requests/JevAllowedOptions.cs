using PitWall.Domain;

namespace PitWall.Jev.Requests;

/// <summary>
/// Opciones que se le ofrecen a Jev tras aplicar las restricciones del motor de reglas: las prohibidas no se envían
/// (docs/jev.md §3.2). Mismo criterio que el filtro de candidatas de Monte Carlo.
/// </summary>
public static class JevAllowedOptions
{
    public static IReadOnlyList<PitCall> PitCalls(IReadOnlyList<Constraint> constraints)
    {
        ArgumentNullException.ThrowIfNull(constraints);

        var allowed = Enum.GetValues<PitCall>()
            .Where(call => !constraints.Any(c => c.ForbiddenCalls.Contains(call)))
            .ToList();

        // Una pit call forzada que otra regla prohíbe (p. ej. pinchazo con bandera roja) no puede cumplirse y no
        // restringe al resto.
        var forced = constraints
            .Select(c => c.ForcedCall)
            .OfType<PitCall>()
            .Where(allowed.Contains)
            .Distinct()
            .ToList();

        return forced.Count > 0 ? forced : allowed;
    }

    public static IReadOnlyList<Compound> Compounds(IReadOnlyList<Constraint> constraints)
    {
        ArgumentNullException.ThrowIfNull(constraints);

        return [.. Enum.GetValues<Compound>().Where(compound => !constraints.Any(c => c.ForbiddenCompounds.Contains(compound)))];
    }

    /// <summary>Modos de energía permitidos (p. ej. sin <c>DEPLOY</c>/<c>OVERTAKE</c> con falla de ERS).</summary>
    public static IReadOnlyList<EnergyMode> EnergyModes(IReadOnlyList<Constraint> constraints)
    {
        ArgumentNullException.ThrowIfNull(constraints);

        return [.. Enum.GetValues<EnergyMode>().Where(mode => !constraints.Any(c => c.ForbiddenEnergyModes.Contains(mode)))];
    }
}
