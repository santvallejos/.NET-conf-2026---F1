using PitWall.Domain;
using PitWall.Strategy.Degradation;
using PitWall.Strategy.Energy;
using PitWall.Strategy.MonteCarlo;
using PitWall.Strategy.Rules;

namespace PitWall.Strategy.Workers;

/// <summary>
/// Cálculo de estrategia de un piloto al cerrar una vuelta (docs/architecture.md §3): degradación de toda la
/// parrilla, energía, reglas (restricciones y razones) y Monte Carlo sobre las candidatas permitidas.
/// </summary>
public sealed class StrategyCalculator
{
    // Auto con varias vueltas menos que el líder y sin gap: abandonó y no cuenta como tráfico.
    private const int RetiredLapDeficit = 3;

    // Gap supuesto respecto al de adelante para un auto sin dato de intervalo (p. ej. doblado).
    private const double UnknownGapStepSeconds = 1.0;

    // Ritmo base si nadie tiene vueltas con tiempo todavía: solo importan los tiempos relativos.
    private const double NoDataLapTimeSeconds = 90.0;

    // Vueltas recientes con las que se estima el ritmo de un auto sin modelo de degradación.
    private const int RecentLapsForFallback = 5;

    // lane_duration fuera de este rango (drive-through, auto guardado) no es una parada normal.
    private const double MaxPitLaneSeconds = 60.0;

    private static readonly Compound[] DryCompounds = [Compound.Soft, Compound.Medium, Compound.Hard];

    private readonly TyreDegradationModel _degradation;
    private readonly DegradationOptions _degradationOptions;
    private readonly RulesEngine _rules;
    private readonly MonteCarloSimulator _simulator;
    private readonly MonteCarloOptions _options;
    private readonly TimeProvider _timeProvider;

    public StrategyCalculator(
        TyreDegradationModel degradation,
        DegradationOptions degradationOptions,
        RulesEngine rules,
        MonteCarloSimulator simulator,
        MonteCarloOptions options,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(degradation);
        ArgumentNullException.ThrowIfNull(degradationOptions);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(simulator);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _degradation = degradation;
        _degradationOptions = degradationOptions;
        _rules = rules;
        _simulator = simulator;
        _options = options;
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
        var lap = car.Lap;
        var running = state.Cars
            .Where(candidate => candidate.DriverNumber == driverNumber || !IsRetired(candidate, state))
            .ToList();
        var paces = Paces(context, running);
        var tyre = paces[driverNumber];
        var compounds = FreshTyreModels(context, driverNumber, tyre);

        var overtakeDisabled = OvertakeSignals.IsOvertakeDisabled(context.RaceControl);
        var energy = context.Energy.TryGetValue(driverNumber, out var estimate)
            ? estimate
            : new EnergyEstimate(100.0, 0, 0, EnergyModel.IsOvertakeAvailable(car.Interval, overtakeDisabled), IsEstimated: true);
        var (pitLossMean, pitLossSigma) = PitLoss(context.PitLaneDurations);
        var rainRisk = RainSignals.RainRisk(context.RaceControl);

        var index = running.FindIndex(candidate => candidate.DriverNumber == driverNumber);
        var features = new RuleFeatures(
            state.Track,
            RaceControlSignals.IsPitLaneClosed(context.RaceControl),
            state.Weather.Rainfall ? RainIntensity.Light : RainIntensity.None,
            context.PreviousRainfall ? RainIntensity.Light : RainIntensity.None,
            RainForecast: rainRisk > 0,
            context.TrackWet,
            UsedCompounds(context, driverNumber),
            tyre,
            energy,
            GapBehind: index + 1 < running.Count ? running[index + 1].Interval : null,
            RivalAheadPitted: index > 0 && running[index - 1].InPit,
            ExitGap(running, car, pitLossMean),
            context.PuncturedDrivers.Contains(driverNumber),
            DamageSeverity.None,
            RivalIncident: false,
            ErsFailure: false);
        var constraints = _rules.EvaluateConstraints(features);
        var reasons = _rules.EvaluateReasons(features);

        var candidates = CandidateGenerator.Generate(lap, context.TotalLaps, tyre, compounds, constraints);
        var input = new MonteCarloInput(
            driverNumber,
            lap,
            context.TotalLaps,
            Field(context, running, paces),
            compounds,
            state.Weather.Rainfall,
            rainRisk,
            state.Track.SafetyCar ? Neutralisation.SafetyCar
                : state.Track.VirtualSafetyCar ? Neutralisation.VirtualSafetyCar
                : Neutralisation.None,
            pitLossMean,
            pitLossSigma,
            OvertakeBase(context),
            OvertakeModeEnabled: !overtakeDisabled,
            _degradationOptions.FuelCoefSecondsPerLap,
            features.Puncture,
            _options.Seed);

        var monteCarloStarted = _timeProvider.GetTimestamp();
        IReadOnlyList<StrategyOutcome> outcomes = candidates.Count == 0 ? [] : _simulator.Run(input, candidates);
        var monteCarloDuration = _timeProvider.GetElapsedTime(monteCarloStarted);

        var snapshot = new StrategySnapshot(
            driverNumber,
            lap,
            tyre,
            energy,
            [.. outcomes
                .OrderBy(outcome => outcome.ExpectedPosition)
                .ThenBy(outcome => outcome.ExpectedRaceTimeDelta)
                .ThenBy(outcome => outcome.StrategyId, StringComparer.Ordinal)],
            reasons,
            constraints);
        return new StrategyResult(snapshot, _simulator.Iterations, _timeProvider.GetElapsedTime(started), monteCarloDuration);
    }

    private static bool IsRetired(CarState car, RaceState state) =>
        car.GapToLeader is null && car.Lap < state.LeaderLap - RetiredLapDeficit;

    /// <summary>
    /// Modelo de degradación del stint actual de cada auto. Sin vueltas limpias propias ni de la parrilla con su
    /// compuesto, el ritmo es la mediana de sus últimas vueltas (sin degradación) y, sin vueltas, la de la parrilla.
    /// </summary>
    private Dictionary<int, TyreModel> Paces(StrategyContext context, List<CarState> running)
    {
        var fits = new Dictionary<int, TyreModel?>();
        var recent = new Dictionary<int, double?>();
        foreach (var car in running)
        {
            fits[car.DriverNumber] = _degradation.Fit(context.CompletedLaps, car.DriverNumber, car.Compound, car.TyreAge, context.TotalLaps)?.Tyre;
            recent[car.DriverNumber] = RecentPace(context, car.DriverNumber);
        }

        var known = fits.Values.OfType<TyreModel>().Select(model => model.BaseLapTime)
            .Concat(recent.Values.OfType<double>())
            .ToList();
        var fieldPace = known.Count > 0 ? Median(known) : NoDataLapTimeSeconds;

        return running.ToDictionary(
            car => car.DriverNumber,
            car => fits[car.DriverNumber]
                ?? new TyreModel(
                    car.Compound,
                    car.TyreAge,
                    recent[car.DriverNumber] ?? fieldPace,
                    DegPerLap: 0,
                    _options.FallbackLapSigmaSeconds,
                    TyreDegradationModel.NoCliff,
                    IsFallback: true));
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

    /// <summary>Modelo con goma nueva del piloto por compuesto; el actual siempre está (su propio ritmo si no hay ajuste).</summary>
    private Dictionary<Compound, TyreModel> FreshTyreModels(StrategyContext context, int driverNumber, TyreModel current)
    {
        var models = new Dictionary<Compound, TyreModel>();
        foreach (var compound in Enum.GetValues<Compound>())
        {
            if (_degradation.Fit(context.CompletedLaps, driverNumber, compound, tyreAge: 0, context.TotalLaps) is { } fit)
            {
                models[compound] = fit.Tyre;
            }
        }

        models.TryAdd(current.Compound, current with { TyreAge = 0, LapsToCliff = _degradation.LapsToCliff(current.DegPerLap, 0) });
        return models;
    }

    private static IReadOnlyList<Compound> UsedCompounds(StrategyContext context, int driverNumber) =>
        context.UsedCompounds.TryGetValue(driverNumber, out var used) ? used : [];

    private static List<MonteCarloCar> Field(StrategyContext context, List<CarState> running, Dictionary<int, TyreModel> paces)
    {
        var field = new List<MonteCarloCar>(running.Count);
        var previousGap = 0.0;
        foreach (var car in running)
        {
            var gap = car.GapToLeader ?? (field.Count == 0 ? 0 : previousGap + UnknownGapStepSeconds);
            previousGap = gap;
            var used = UsedCompounds(context, car.DriverNumber).Append(car.Compound).ToHashSet();
            var mandatoryPending = used.All(compound => DryCompounds.Contains(compound)) && used.Count < 2;
            field.Add(new MonteCarloCar(car.DriverNumber, gap, paces[car.DriverNumber], mandatoryPending));
        }

        return field;
    }

    /// <summary>Pit loss de la sesión (<c>lane_duration</c>); con pocas paradas, el de configuración.</summary>
    private (double Mean, double Sigma) PitLoss(IReadOnlyList<double> laneDurations)
    {
        var durations = laneDurations.Where(duration => duration is > 0 and < MaxPitLaneSeconds).ToList();
        if (durations.Count < Math.Max(2, _options.MinPitSamples))
        {
            return (_options.PitLossMeanSeconds, _options.PitLossSigmaSeconds);
        }

        var mean = durations.Average();
        var variance = durations.Sum(duration => (duration - mean) * (duration - mean)) / (durations.Count - 1);
        return (mean, Math.Sqrt(variance));
    }

    /// <summary>Gap al auto más cercano al salir de boxes si se para ahora; null sin gap propio.</summary>
    private static double? ExitGap(List<CarState> running, CarState car, double pitLoss)
    {
        if (car.GapToLeader is not { } gap)
        {
            return null;
        }

        var exit = gap + pitLoss;
        var gaps = running
            .Where(other => other.DriverNumber != car.DriverNumber && other.GapToLeader is not null)
            .Select(other => Math.Abs(other.GapToLeader!.Value - exit))
            .ToList();
        return gaps.Count > 0 ? gaps.Min() : null;
    }

    /// <summary>
    /// Probabilidad base de adelantar escalada por los <c>overtakes</c> de la sesión respecto a una carrera de
    /// referencia (ESTIMADO); con pocas vueltas cerradas, la de configuración.
    /// </summary>
    private double OvertakeBase(StrategyContext context)
    {
        var carLaps = context.CompletedLaps.Count;
        if (carLaps < _options.MinCarLapsForOvertakeRate)
        {
            return _options.OvertakeBaseProbability;
        }

        var rate = (double)context.Overtakes / carLaps;
        return Math.Clamp(_options.OvertakeBaseProbability * rate / _options.OvertakeReferenceRatePerCarLap, 0, _options.MaxOvertakeProbability);
    }

    private static double Median(List<double> values)
    {
        values.Sort();
        var middle = values.Count / 2;
        return values.Count % 2 == 1 ? values[middle] : (values[middle - 1] + values[middle]) / 2.0;
    }
}
