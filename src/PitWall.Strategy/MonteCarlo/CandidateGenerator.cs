using PitWall.Domain;
using PitWall.Strategy.Degradation;
using PitWall.Strategy.Rules;

namespace PitWall.Strategy.MonteCarlo;

/// <summary>
/// Estrategias candidatas de docs/strategy-engine.md §4.1: <c>STAY_OUT</c> y <c>BOX</c> en <c>L..L+3</c> × cada
/// compuesto con modelo que las restricciones permiten.
/// </summary>
public static class CandidateGenerator
{
    public const string StayOutId = "STAY_OUT";

    /// <summary>La ventana de parada es <c>L..L+BoxWindowLaps</c>.</summary>
    public const int BoxWindowLaps = 3;

    private static readonly Compound[] DryCompounds = [Compound.Soft, Compound.Medium, Compound.Hard];

    /// <param name="lap">Vuelta en curso <c>L</c>.</param>
    /// <param name="currentTyre">Modelo del stint actual (su <see cref="TyreModel.LapsToCliff"/> fija la parada de <c>STAY_OUT</c>).</param>
    /// <param name="compounds">Modelo con goma nueva por compuesto; sin modelo no hay candidata con ese compuesto.</param>
    public static IReadOnlyList<StrategyCandidate> Generate(
        int lap,
        int totalLaps,
        TyreModel currentTyre,
        IReadOnlyDictionary<Compound, TyreModel> compounds,
        IReadOnlyList<Constraint> constraints)
    {
        ArgumentNullException.ThrowIfNull(currentTyre);
        ArgumentNullException.ThrowIfNull(compounds);
        ArgumentNullException.ThrowIfNull(constraints);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lap);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(totalLaps);

        var mandatoryPending = constraints.Any(constraint => constraint.RequiresSecondDryCompound);
        var candidates = new List<StrategyCandidate>();

        if (RulesEngine.IsAllowed(constraints, PitCall.StayOut, null))
        {
            candidates.Add(StayOut(lap, totalLaps, currentTyre, compounds, constraints, mandatoryPending));
        }

        // Parar en la última vuelta no cambia nada: la ventana termina en TotalLaps − 1.
        for (var offset = 0; offset <= BoxWindowLaps && lap + offset < totalLaps; offset++)
        {
            var call = offset switch
            {
                0 => PitCall.Box,
                1 => PitCall.BoxNextLap,
                _ => PitCall.StayOut,
            };

            foreach (var compound in compounds.Keys.Order())
            {
                if (!RulesEngine.IsAllowed(constraints, call, compound)
                    || !SatisfiesMandatory(mandatoryPending, currentTyre.Compound, compound))
                {
                    continue;
                }

                var boxLap = lap + offset;
                var id = offset == 0 ? $"BOX_L_{Name(compound)}" : $"BOX_L+{offset}_{Name(compound)}";
                candidates.Add(new StrategyCandidate(id, call, boxLap, compound, boxLap, compound));
            }
        }

        return candidates;
    }

    /// <summary>
    /// Sin parada en la ventana: se sigue hasta el cliff (o hasta el final). Si el cliff llega antes del final, o
    /// falta el segundo compuesto seco, se simula la parada ahí con el compuesto de mejor ritmo medio permitido.
    /// </summary>
    private static StrategyCandidate StayOut(
        int lap,
        int totalLaps,
        TyreModel currentTyre,
        IReadOnlyDictionary<Compound, TyreModel> compounds,
        IReadOnlyList<Constraint> constraints,
        bool mandatoryPending)
    {
        var earliest = lap + BoxWindowLaps + 1;
        int? stopLap = currentTyre.LapsToCliff == TyreDegradationModel.NoCliff || lap + currentTyre.LapsToCliff >= totalLaps
            ? null
            : Math.Max(earliest, lap + currentTyre.LapsToCliff);
        if (stopLap is null && mandatoryPending)
        {
            stopLap = totalLaps - 1;
        }

        if (stopLap is not { } stop || stop < earliest || stop >= totalLaps)
        {
            return new StrategyCandidate(StayOutId, PitCall.StayOut, null, null, null, null);
        }

        var stintLaps = totalLaps - stop;
        Compound? best = compounds
            .Where(pair => !constraints.Any(constraint => constraint.ForbiddenCompounds.Contains(pair.Key))
                && SatisfiesMandatory(mandatoryPending, currentTyre.Compound, pair.Key))
            .OrderBy(pair => pair.Value.BaseLapTime + (pair.Value.DegPerLap * (stintLaps - 1) / 2.0))
            .ThenBy(pair => pair.Key)
            .Select(pair => (Compound?)pair.Key)
            .FirstOrDefault();

        return best is null
            ? new StrategyCandidate(StayOutId, PitCall.StayOut, null, null, null, null)
            : new StrategyCandidate(StayOutId, PitCall.StayOut, null, null, stop, best);
    }

    // Con la regla de dos compuestos secos pendiente, volver a montar el mismo compuesto seco no la cumple.
    private static bool SatisfiesMandatory(bool mandatoryPending, Compound current, Compound fitted) =>
        !mandatoryPending || fitted != current || !DryCompounds.Contains(fitted);

    private static string Name(Compound compound) => compound.ToString().ToUpperInvariant();
}
