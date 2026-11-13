using PitWall.Domain;
using PitWall.Strategy.Degradation;

namespace PitWall.Tests.Strategy.Degradation;

/// <summary>Vueltas sintéticas con degradación y combustible conocidos: <c>base + deg · edad + fuel · restantes</c>.</summary>
internal static class SyntheticLaps
{
    public const int TotalLaps = 70;

    public const double FuelCoef = 0.03;

    /// <summary>Stint limpio de <paramref name="count"/> vueltas desde <paramref name="firstLap"/>, goma nueva.</summary>
    public static List<LapSample> Stint(
        int driverNumber,
        Compound compound,
        int firstLap,
        int count,
        double baseLapTime,
        double deg,
        double noise = 0)
    {
        var laps = new List<LapSample>(count);
        for (var lap = firstLap; lap < firstLap + count; lap++)
        {
            var age = lap - firstLap + 1;
            // Ruido determinista acotado a ±noise (sin aleatoriedad en el test).
            var time = baseLapTime + (deg * age) + (FuelCoef * (TotalLaps - lap)) + (noise * Math.Sin(lap * 1.7));
            laps.Add(new LapSample(driverNumber, lap, compound, age, time, IsPitOutLap: false, IsPitInLap: false, IsNeutralised: false));
        }

        return laps;
    }

    public static DegradationOptions Options(double fuelCoef = FuelCoef, double cliff = 1.5, int minCleanLaps = 4) =>
        new() { FuelCoefSecondsPerLap = fuelCoef, CliffDeltaSeconds = cliff, MinCleanLaps = minCleanLaps };
}
