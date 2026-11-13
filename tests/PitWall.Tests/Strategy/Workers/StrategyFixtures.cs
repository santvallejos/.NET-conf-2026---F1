using PitWall.Domain;
using PitWall.Strategy.Degradation;
using PitWall.Strategy.PitWindow;
using PitWall.Strategy.Workers;
using PitWall.Tests.Strategy.Degradation;

namespace PitWall.Tests.Strategy.Workers;

/// <summary>Carrera sintética para el cálculo de estrategia por piloto.</summary>
internal static class StrategyFixtures
{
    public const int Focus = 43;

    public const int TotalLaps = SyntheticLaps.TotalLaps;

    public static StrategyCalculator Calculator(PitCallOptions? pitCallOptions = null) => new(
        new TyreDegradationModel(SyntheticLaps.Options()),
        SyntheticLaps.Options(),
        pitCallOptions ?? new PitCallOptions(),
        TimeProvider.System);

    /// <summary>
    /// Carrera seca de 20 autos en la vuelta <paramref name="lap"/>: todos con medio desde la vuelta 2 y el foco con un
    /// stint previo de blando; tres rivales con duro para que la parrilla tenga modelo de los tres secos.
    /// </summary>
    public static StrategyContext Race(int lap = 20)
    {
        var laps = new List<LapSample>();
        var cars = new List<CarState>();
        for (var position = 1; position <= 20; position++)
        {
            var driver = position == 5 ? Focus : position;
            var baseLapTime = 72.0 + (0.05 * position);
            if (driver == Focus)
            {
                laps.AddRange(SyntheticLaps.Stint(driver, Compound.Soft, firstLap: 2, count: 8, baseLapTime - 0.4, deg: 0.1, noise: 0.1));
                laps.AddRange(SyntheticLaps.Stint(driver, Compound.Medium, firstLap: 10, count: lap - 10, baseLapTime, deg: 0.06, noise: 0.1));
                cars.Add(new CarState(driver, position, lap, (position - 1) * 1.5, 1.5, Compound.Medium, lap - 10, InPit: false));
                continue;
            }

            var compound = position % 6 == 0 ? Compound.Hard : Compound.Medium;
            laps.AddRange(SyntheticLaps.Stint(driver, compound, firstLap: 2, count: lap - 2, baseLapTime, deg: 0.05, noise: 0.1));
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
            [],
            PitLaneDurations: [21.5, 22.3, 21.9, 22.8],
            new Dictionary<int, EnergyEstimate>());
    }
}
