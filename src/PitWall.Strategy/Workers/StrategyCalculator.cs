using PitWall.Domain;
using PitWall.Strategy.Degradation;
using PitWall.Strategy.Energy;
using PitWall.Strategy.PitWindow;

namespace PitWall.Strategy.Workers;

/// <summary>
/// Cálculo de estrategia de un piloto al cerrar una vuelta (docs/architecture.md §3): degradación del stint actual,
/// energía y pit loss de la sesión.
/// </summary>
public sealed class StrategyCalculator
{
    // Ritmo base si nadie tiene vueltas con tiempo todavía: solo completa el modelo.
    private const double NoDataLapTimeSeconds = 90.0;

    // Vueltas recientes con las que se estima el ritmo de un auto sin modelo de degradación.
    private const int RecentLapsForFallback = 5;

    // lane_duration fuera de este rango (drive-through, auto guardado) no es una parada normal.
    private const double MaxPitLaneSeconds = 60.0;

    private readonly TyreDegradationModel _degradation;
    private readonly DegradationOptions _degradationOptions;
    private readonly PitCallOptions _pitCallOptions;
    private readonly TimeProvider _timeProvider;

    public StrategyCalculator(
        TyreDegradationModel degradation,
        DegradationOptions degradationOptions,
        PitCallOptions pitCallOptions,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(degradation);
        ArgumentNullException.ThrowIfNull(degradationOptions);
        ArgumentNullException.ThrowIfNull(pitCallOptions);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _degradation = degradation;
        _degradationOptions = degradationOptions;
        _pitCallOptions = pitCallOptions;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Estrategia de <paramref name="driverNumber"/> en su vuelta en curso; <c>null</c> si no está en carrera o la
    /// carrera ya terminó para él.
    /// </summary>
    public StrategyResult? Calculate(StrategyContext context, int driverNumber)
    {
        ArgumentNullException.ThrowIfNull(context);

        var state = context.State;
        var car = state.Cars.FirstOrDefault(candidate => candidate.DriverNumber == driverNumber);
        if (car is null || car.Lap < 1 || car.Lap > context.TotalLaps)
        {
            return null;
        }

        var started = _timeProvider.GetTimestamp();
        var tyre = CurrentTyre(context, car);
        var overtakeDisabled = OvertakeSignals.IsOvertakeDisabled(context.RaceControl);
        var energy = context.Energy.TryGetValue(driverNumber, out var estimate)
            ? estimate
            : new EnergyEstimate(100.0, 0, 0, EnergyModel.IsOvertakeAvailable(car.Interval, overtakeDisabled), IsEstimated: true);

        var snapshot = new StrategySnapshot(driverNumber, car.Lap, tyre, energy)
        {
            PitLossSeconds = PitLoss(context.PitLaneDurations),
        };
        return new StrategyResult(snapshot, _timeProvider.GetElapsedTime(started));
    }

    /// <summary>
    /// Modelo de degradación del stint actual. Sin vueltas limpias propias ni de la parrilla con su compuesto, el ritmo
    /// es la mediana de sus últimas vueltas (sin degradación) y, sin vueltas, la de la parrilla.
    /// </summary>
    private TyreModel CurrentTyre(StrategyContext context, CarState car)
    {
        if (_degradation.Fit(context.CompletedLaps, car.DriverNumber, car.Compound, car.TyreAge, context.TotalLaps) is { } fit)
        {
            return fit.Tyre;
        }

        var pace = RecentPace(context, car.DriverNumber) ?? FieldPace(context);
        return new TyreModel(car.Compound, car.TyreAge, pace, DegPerLap: 0, ResidualSigma: 0, TyreDegradationModel.NoCliff, IsFallback: true);
    }

    private double? RecentPace(StrategyContext context, int driverNumber)
    {
        var times = context.CompletedLaps
            .Where(lap => lap.DriverNumber == driverNumber && lap.LapTime is > 0 && !lap.IsPitInLap && !lap.IsPitOutLap)
            .OrderByDescending(lap => lap.LapNumber)
            .Take(RecentLapsForFallback)
            .Select(lap => lap.LapTime!.Value - (_degradationOptions.FuelCoefSecondsPerLap * Math.Max(0, context.TotalLaps - lap.LapNumber)))
            .ToList();
        return times.Count > 0 ? Median(times) : null;
    }

    private double FieldPace(StrategyContext context)
    {
        var paces = context.CompletedLaps
            .Select(lap => lap.DriverNumber)
            .Distinct()
            .Select(driver => RecentPace(context, driver))
            .OfType<double>()
            .ToList();
        return paces.Count > 0 ? Median(paces) : NoDataLapTimeSeconds;
    }

    /// <summary>Pit loss medio de la sesión (<c>lane_duration</c>); con pocas paradas, el de configuración.</summary>
    private double PitLoss(IReadOnlyList<double> laneDurations)
    {
        var durations = laneDurations.Where(duration => duration is > 0 and < MaxPitLaneSeconds).ToList();
        return durations.Count < _pitCallOptions.MinPitSamples ? _pitCallOptions.DefaultPitLossSeconds : durations.Average();
    }

    private static double Median(List<double> values)
    {
        values.Sort();
        var middle = values.Count / 2;
        return values.Count % 2 == 1 ? values[middle] : (values[middle - 1] + values[middle]) / 2.0;
    }
}
