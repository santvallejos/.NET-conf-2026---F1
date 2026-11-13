using PitWall.Domain;

namespace PitWall.Strategy.Degradation;

/// <summary>
/// Degradación por (piloto, compuesto) sobre vueltas reales (docs/strategy-engine.md §1, ADR-014):
/// <c>lap_time = base + deg · tyre_age + fuel_coef · laps_remaining + ε</c>. El término de combustible se
/// descuenta antes de la regresión lineal para aislar <c>deg</c>.
/// </summary>
public sealed class TyreDegradationModel
{
    /// <summary><see cref="TyreModel.LapsToCliff"/> cuando la goma no se degrada (deg ≤ 0).</summary>
    public const int NoCliff = int.MaxValue;

    private const double OutlierSigmas = 3.0;

    // Diferencias por debajo de un microsegundo son ruido de redondeo, no tráfico ni errores.
    private const double ResidualTolerance = 1e-6;

    private readonly DegradationOptions _options;

    public TyreDegradationModel(DegradationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MinCleanLaps, 3);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.CliffDeltaSeconds);

        _options = options;
    }

    /// <summary>
    /// Ajusta el modelo de <paramref name="driverNumber"/> con <paramref name="compound"/> usando sus vueltas
    /// limpias de <paramref name="laps"/> (vueltas completadas de toda la parrilla en la sesión). Con menos de
    /// <see cref="DegradationOptions.MinCleanLaps"/> usa la media del compuesto del resto de la parrilla.
    /// </summary>
    /// <param name="tyreAge">Edad actual del neumático, para la vida restante.</param>
    /// <param name="totalLaps">Vueltas de la carrera, para <c>laps_remaining</c>.</param>
    /// <returns><c>null</c> si ni el piloto ni la parrilla tienen vueltas limpias suficientes con el compuesto.</returns>
    public DegradationFit? Fit(IReadOnlyList<LapSample> laps, int driverNumber, Compound compound, int tyreAge, int totalLaps)
    {
        ArgumentNullException.ThrowIfNull(laps);
        ArgumentOutOfRangeException.ThrowIfNegative(tyreAge);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(totalLaps);

        var points = Classify(laps.Where(lap => lap.DriverNumber == driverNumber && lap.Compound == compound), totalLaps);
        var clean = points.Where(point => point.IsClean).ToList();
        if (clean.Count >= _options.MinCleanLaps && Regress(clean) is { } own)
        {
            return new DegradationFit(
                new TyreModel(compound, tyreAge, own.Base, own.Deg, own.Sigma, LapsToCliff(own.Deg, tyreAge), IsFallback: false),
                points,
                FallbackDrivers: 0);
        }

        var grid = laps
            .Where(lap => lap.DriverNumber != driverNumber && lap.Compound == compound)
            .GroupBy(lap => lap.DriverNumber)
            .Select(group => Classify(group, totalLaps).Where(point => point.IsClean).ToList())
            .Where(driverClean => driverClean.Count >= _options.MinCleanLaps)
            .Select(Regress)
            .OfType<Regression>()
            .ToList();
        if (grid.Count == 0)
        {
            return null;
        }

        var deg = grid.Average(model => model.Deg);
        // El ritmo base sale de las vueltas limpias propias si las hay; la pendiente siempre es la de la parrilla.
        var baseLapTime = clean.Count > 0
            ? clean.Average(point => point.FuelCorrectedLapTime!.Value - (deg * point.TyreAge))
            : grid.Average(model => model.Base);

        return new DegradationFit(
            new TyreModel(compound, tyreAge, baseLapTime, deg, grid.Average(model => model.Sigma), LapsToCliff(deg, tyreAge), IsFallback: true),
            points,
            grid.Count);
    }

    /// <summary>
    /// Vueltas hasta que <c>deg · tyre_age</c> supere <see cref="DegradationOptions.CliffDeltaSeconds"/> respecto a
    /// goma nueva; 0 si ya lo superó y <see cref="NoCliff"/> si la goma no se degrada.
    /// </summary>
    public int LapsToCliff(double degPerLap, int tyreAge)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tyreAge);

        var cliffAge = CliffTyreAge(degPerLap);
        return cliffAge == NoCliff ? NoCliff : Math.Max(0, cliffAge - tyreAge);
    }

    /// <summary>Primera edad del neumático con pérdida mayor a <see cref="DegradationOptions.CliffDeltaSeconds"/>.</summary>
    public int CliffTyreAge(double degPerLap)
    {
        if (degPerLap <= 0)
        {
            return NoCliff;
        }

        var ages = Math.Floor(_options.CliffDeltaSeconds / degPerLap) + 1;
        return ages >= NoCliff ? NoCliff : (int)ages;
    }

    private List<DegradationPoint> Classify(IEnumerable<LapSample> laps, int totalLaps)
    {
        var points = laps
            .OrderBy(lap => lap.LapNumber)
            .Select(lap =>
            {
                double? corrected = lap.LapTime is { } time
                    ? time - (_options.FuelCoefSecondsPerLap * Math.Max(0, totalLaps - lap.LapNumber))
                    : null;
                return new DegradationPoint(lap.LapNumber, lap.TyreAge, lap.LapTime, corrected, Exclude(lap));
            })
            .ToList();

        MarkOutliers(points);
        return points;
    }

    private static LapExclusion Exclude(LapSample lap) => lap switch
    {
        { LapTime: null or <= 0 } => LapExclusion.NoLapTime,
        { LapNumber: <= 1 } => LapExclusion.FirstLap,
        { IsPitOutLap: true } => LapExclusion.PitOutLap,
        { IsPitInLap: true } => LapExclusion.PitInLap,
        { IsNeutralised: true } => LapExclusion.Neutralised,
        _ => LapExclusion.None,
    };

    /// <summary>
    /// Outliers > 3σ por residuo eliminado: cada vuelta se compara con la recta ajustada sin ella y con el σ de
    /// las demás, para que una vuelta lenta no infle el σ que la juzga. Se descarta la peor y se repite.
    /// </summary>
    private static void MarkOutliers(List<DegradationPoint> points)
    {
        while (true)
        {
            var clean = points.Where(point => point.IsClean).ToList();
            if (clean.Count < 4)
            {
                return;
            }

            DegradationPoint? worst = null;
            var worstScore = 0.0;
            foreach (var candidate in clean)
            {
                if (Regress([.. clean.Where(point => !ReferenceEquals(point, candidate))]) is not { } others)
                {
                    continue;
                }

                var error = Math.Abs(candidate.FuelCorrectedLapTime!.Value - others.Predict(candidate.TyreAge));
                if (error <= ResidualTolerance || error <= OutlierSigmas * others.Sigma)
                {
                    continue;
                }

                var score = others.Sigma > 0 ? error / others.Sigma : double.PositiveInfinity;
                if (worst is null || score > worstScore)
                {
                    worst = candidate;
                    worstScore = score;
                }
            }

            if (worst is null)
            {
                return;
            }

            points[points.IndexOf(worst)] = worst with { Exclusion = LapExclusion.Outlier };
        }
    }

    /// <summary>Mínimos cuadrados de tiempo corregido sobre edad; <c>null</c> si todas las edades son iguales.</summary>
    private static Regression? Regress(IReadOnlyList<DegradationPoint> clean)
    {
        if (clean.Count < 2)
        {
            return null;
        }

        var meanX = clean.Average(point => (double)point.TyreAge);
        var meanY = clean.Average(point => point.FuelCorrectedLapTime!.Value);
        var sxx = clean.Sum(point => (point.TyreAge - meanX) * (point.TyreAge - meanX));
        if (sxx == 0)
        {
            return null;
        }

        var sxy = clean.Sum(point => (point.TyreAge - meanX) * (point.FuelCorrectedLapTime!.Value - meanY));
        var deg = sxy / sxx;
        var intercept = meanY - (deg * meanX);
        var sse = clean.Sum(point =>
        {
            var residual = point.FuelCorrectedLapTime!.Value - (intercept + (deg * point.TyreAge));
            return residual * residual;
        });
        var sigma = clean.Count > 2 ? Math.Sqrt(sse / (clean.Count - 2)) : 0;
        return new Regression(intercept, deg, sigma);
    }

    private sealed record Regression(double Base, double Deg, double Sigma)
    {
        public double Predict(int tyreAge) => Base + (Deg * tyreAge);
    }
}
