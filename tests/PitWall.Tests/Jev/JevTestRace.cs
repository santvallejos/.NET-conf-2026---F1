using PitWall.Domain;
using PitWall.Jev.Requests;

namespace PitWall.Tests.Jev;

/// <summary>Estados de carrera y estrategias sintéticos para armar requests de Jev.</summary>
internal static class JevTestRace
{
    public const int FocusDriver = 43;
    public const int LapsTotal = 71;

    public static readonly DateTimeOffset RaceStart = new(2026, 11, 8, 17, 0, 0, TimeSpan.Zero);

    /// <summary>Estado de docs/jev.md §3.3: vuelta 60, P13, lluvia, blandos con 42 vueltas.</summary>
    public static RaceState StateLap60() => new(
        JevTestRequests.SessionKey,
        RaceStart.AddMinutes(90),
        LeaderLap: 61,
        [
            new CarState(44, 12, 60, 20.0, 2.5, Compound.Medium, 20, InPit: false),
            new CarState(FocusDriver, 13, 60, 21.8, 1.8, Compound.Soft, 42, InPit: false),
            new CarState(12, 14, 60, 22.7, 0.9, Compound.Hard, 30, InPit: false),
        ],
        new WeatherState(AirTemp: 22.0, TrackTemp: 29.1, Humidity: 81, Rainfall: true),
        new TrackStatus("GREEN", SafetyCar: false, VirtualSafetyCar: false),
        []);

    public static StrategySnapshot SnapshotLap60(params Constraint[] constraints) => new(
        FocusDriver,
        60,
        new TyreModel(Compound.Soft, 42, BaseLapTime: 80.0, DegPerLap: 0.11, ResidualSigma: 0.2, LapsToCliff: 3, IsFallback: false),
        new EnergyEstimate(SocPct: 54, HarvestedLapMj: 1.0, DeployedLapMj: 1.0, OvertakeAvailable: false, IsEstimated: true),
        [
            Outcome("STAY_OUT", PitCall.StayOut, null, null, 14.6, 0.08),
            Outcome("BOX_L_INTERMEDIATE", PitCall.Box, 60, Compound.Intermediate, 11.9, 0.61),
            Outcome("BOX_L+1_INTERMEDIATE", PitCall.BoxNextLap, 61, Compound.Intermediate, 12.4, 0.49),
        ],
        [Reason.RainStarting, Reason.TyreCliffNear, Reason.LowGrip],
        constraints);

    public static JevStrategyInput StrategyInputLap60(StrategySnapshot? snapshot = null, int topOutcomes = 5) => new(
        JevTestRequests.SessionKey,
        StateLap60(),
        snapshot ?? SnapshotLap60(),
        LapsTotal,
        ["LOW GRIP CONDITIONS", "OVERTAKE DISABLED"],
        [],
        topOutcomes);

    public static StrategyOutcome Outcome(string id, PitCall call, int? boxLap, Compound? compound, double expectedPosition, double pGain) =>
        new(id, call, boxLap, compound, expectedPosition, new Dictionary<int, double> { [(int)Math.Round(expectedPosition)] = 1.0 }, pGain, 1 - pGain, 0.0);
}
