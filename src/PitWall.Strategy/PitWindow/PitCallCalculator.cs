using PitWall.Domain;

namespace PitWall.Strategy.PitWindow;

/// <summary>Resultado de parar ahora con un pit loss dado.</summary>
/// <param name="PitLossSeconds">Tiempo que se pierde en boxes respecto a seguir en pista.</param>
/// <param name="ExitPosition">Posición estimada al salir de boxes.</param>
/// <param name="PositionsLost">Autos de atrás que pasan al foco mientras está en boxes.</param>
public sealed record PitExit(double PitLossSeconds, int ExitPosition, int PositionsLost);

/// <summary>Llamada a boxes del foco si entra ahora: en condiciones normales, bajo SC y bajo VSC.</summary>
public sealed record PitCallEstimate(int CurrentPosition, PitExit Normal, PitExit SafetyCar, PitExit Vsc);

/// <summary>
/// Posición al salir de boxes (docs/strategy-engine.md §4). Determinista: no simula, solo compara el pit loss con la
/// diferencia acumulada (suma de intervalos) entre el foco y cada auto de atrás. Un auto lo pasa si esa diferencia
/// es menor que el pit loss.
/// </summary>
public static class PitCallCalculator
{
    /// <summary>
    /// Estimación para <paramref name="focusDriver"/>; <c>null</c> si no está en <paramref name="cars"/>.
    /// Un auto de atrás sin intervalo (doblado o sin dato) corta la cuenta: no se puede saber si está dentro.
    /// </summary>
    public static PitCallEstimate? Estimate(IReadOnlyList<CarState> cars, int focusDriver, double pitLossSeconds, PitCallOptions options)
    {
        ArgumentNullException.ThrowIfNull(cars);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegative(pitLossSeconds);

        var ordered = cars.OrderBy(car => car.Position).ToList();
        var index = ordered.FindIndex(car => car.DriverNumber == focusDriver);
        if (index < 0)
        {
            return null;
        }

        var focus = ordered[index];
        var behind = ordered.Skip(index + 1).ToList();
        return new PitCallEstimate(
            focus.Position,
            Exit(focus.Position, behind, pitLossSeconds),
            Exit(focus.Position, behind, pitLossSeconds * options.ScPitLossFactor),
            Exit(focus.Position, behind, pitLossSeconds * options.VscPitLossFactor));
    }

    private static PitExit Exit(int position, List<CarState> behind, double pitLoss)
    {
        var lost = 0;
        var gap = 0.0;
        foreach (var car in behind)
        {
            if (car.Interval is not { } interval)
            {
                break;
            }

            gap += interval;
            if (gap >= pitLoss)
            {
                break;
            }

            lost++;
        }

        return new PitExit(pitLoss, position + lost, lost);
    }
}
