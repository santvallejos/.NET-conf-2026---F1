using PitWall.Domain;
using PitWall.Strategy.Degradation;
using PitWall.Strategy.MonteCarlo;
using PitWall.Strategy.Rules;
using PitWall.Strategy.Workers;
using PitWall.Tests.Strategy.Degradation;

namespace PitWall.Tests.Strategy.MonteCarlo;

/// <summary>Parrillas y carreras sintéticas para Monte Carlo (1000 iteraciones y semilla fija salvo que se pida otra cosa).</summary>
internal static class MonteCarloFixtures
{
    public const int Focus = 43;

    public const int TotalLaps = SyntheticLaps.TotalLaps;

    public const int Seed = 1412;

    public static MonteCarloOptions Options(int iterations = 1000) => new() { Iterations = iterations, Seed = Seed };

    public static TyreModel Tyre(Compound compound, int age, double baseLapTime = 72.0, double deg = 0.05, double sigma = 0.2, int lapsToCliff = 20) =>
        new(compound, age, baseLapTime, deg, sigma, lapsToCliff, IsFallback: false);

    /// <summary>Parrilla de <paramref name="cars"/> autos a 1,5 s entre sí con el foco en <paramref name="focusPosition"/>.</summary>
    public static MonteCarloInput Input(int lap = 30, int cars = 10, int focusPosition = 5, TyreModel? focusTyre = null, bool puncture = false, int seed = Seed)
    {
        var field = new List<MonteCarloCar>(cars);
        var rival = 1;
        for (var position = 1; position <= cars; position++)
        {
            var driver = position == focusPosition ? Focus : rival++;
            var tyre = position == focusPosition
                ? focusTyre ?? Tyre(Compound.Medium, age: 20)
                : Tyre(Compound.Medium, age: 20, baseLapTime: 72.0 + (0.05 * position));
            field.Add(new MonteCarloCar(driver, (position - 1) * 1.5, tyre, MandatoryStopPending: false));
        }

        return new MonteCarloInput(
            Focus,
            lap,
            TotalLaps,
            field,
            new Dictionary<Compound, TyreModel>
            {
                [Compound.Soft] = Tyre(Compound.Soft, age: 0, baseLapTime: 71.6, deg: 0.09),
                [Compound.Medium] = Tyre(Compound.Medium, age: 0),
                [Compound.Hard] = Tyre(Compound.Hard, age: 0, baseLapTime: 72.4, deg: 0.03),
            },
            Raining: false,
            RainRisk: 0,
            Neutralisation.None,
            PitLossMean: 22.0,
            PitLossSigma: 1.0,
            OvertakeBaseProbability: 0.3,
            OvertakeModeEnabled: true,
            FuelCoefSecondsPerLap: SyntheticLaps.FuelCoef,
            puncture,
            seed);
    }

    public static StrategyCandidate StayOut() => new(CandidateGenerator.StayOutId, PitCall.StayOut, null, null, null, null);

    public static StrategyCandidate Box(int lap, Compound compound, string id) => new(id, PitCall.Box, lap, compound, lap, compound);

    public static StrategyCalculator Calculator(int iterations = 1000) => new(
        new TyreDegradationModel(SyntheticLaps.Options()),
        SyntheticLaps.Options(),
        new RulesEngine(new RulesOptions()),
        new MonteCarloSimulator(Options(iterations)),
        Options(iterations),
        TimeProvider.System);

    /// <summary>
    /// Carrera seca de 20 autos en la vuelta <paramref name="lap"/>: todos con medio desde la vuelta 2 y el foco con un
    /// stint previo de blando; tres rivales con duro para que la parrilla tenga modelo de los tres secos.
    /// </summary>
    public static StrategyContext Race(int lap = 20, IReadOnlySet<int>? punctured = null)
    {
        var laps = new List<LapSample>();
        var cars = new List<CarState>();
        var used = new Dictionary<int, IReadOnlyList<Compound>>();
        for (var position = 1; position <= 20; position++)
        {
            var driver = position == 5 ? Focus : position;
            var baseLapTime = 72.0 + (0.05 * position);
            if (driver == Focus)
            {
                laps.AddRange(SyntheticLaps.Stint(driver, Compound.Soft, firstLap: 2, count: 8, baseLapTime - 0.4, deg: 0.1, noise: 0.1));
                laps.AddRange(SyntheticLaps.Stint(driver, Compound.Medium, firstLap: 10, count: lap - 10, baseLapTime, deg: 0.06, noise: 0.1));
                used[driver] = [Compound.Soft, Compound.Medium];
                cars.Add(new CarState(driver, position, lap, (position - 1) * 1.5, 1.5, Compound.Medium, lap - 10, InPit: false));
                continue;
            }

            var compound = position % 6 == 0 ? Compound.Hard : Compound.Medium;
            laps.AddRange(SyntheticLaps.Stint(driver, compound, firstLap: 2, count: lap - 2, baseLapTime, deg: 0.05, noise: 0.1));
            used[driver] = [compound];
            cars.Add(new CarState(driver, position, lap, position == 1 ? null : (position - 1) * 1.5, position == 1 ? null : 1.5, compound, lap - 2, InPit: false));
        }

        var state = new RaceState(
            11412,
            new DateTimeOffset(2026, 11, 8, 17, 30, 0, TimeSpan.Zero),
            lap,
            cars,
            new WeatherState(24, 38, 60, Rainfall: false),
            new TrackStatus("GREEN", SafetyCar: false, VirtualSafetyCar: false),
            []);

        return new StrategyContext(
            state,
            TotalLaps,
            laps,
            used,
            [],
            PreviousRainfall: false,
            TrackWet: false,
            PitLaneDurations: [21.5, 22.3, 21.9, 22.8],
            Overtakes: 20,
            new Dictionary<int, EnergyEstimate>(),
            punctured ?? new HashSet<int>());
    }
}
