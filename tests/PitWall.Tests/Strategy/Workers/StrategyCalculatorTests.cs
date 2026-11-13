using PitWall.Domain;
using PitWall.Strategy.Degradation;
using PitWall.Strategy.PitWindow;

namespace PitWall.Tests.Strategy.Workers;

public sealed class StrategyCalculatorTests
{
    private const int Lap = 20;

    [Fact]
    public void Calculate_DryRace_OwnTyreModelDefaultEnergyAndSessionPitLoss()
    {
        var result = StrategyFixtures.Calculator().Calculate(StrategyFixtures.Race(Lap), StrategyFixtures.Focus);

        Assert.NotNull(result);
        var snapshot = result.Snapshot;
        Assert.Equal(StrategyFixtures.Focus, snapshot.DriverNumber);
        Assert.Equal(Lap, snapshot.Lap);
        Assert.Equal(Compound.Medium, snapshot.Tyre.Compound);
        Assert.Equal(Lap - 10, snapshot.Tyre.TyreAge);
        Assert.False(snapshot.Tyre.IsFallback);
        Assert.InRange(snapshot.Tyre.DegPerLap, 0.04, 0.08);
        // Sin estimación del piloto: batería llena (ESTIMADO).
        Assert.Equal(100.0, snapshot.Energy.SocPct);
        Assert.True(snapshot.Energy.IsEstimated);
        // Media de las 4 paradas de la sesión.
        Assert.Equal(22.125, snapshot.PitLossSeconds!.Value, 9);
    }

    [Fact]
    public void Calculate_SessionPitLoss_FeedsPitCallOnRealState()
    {
        var context = StrategyFixtures.Race(Lap);
        var pitLoss = StrategyFixtures.Calculator().Calculate(context, StrategyFixtures.Focus)!.Snapshot.PitLossSeconds!.Value;

        var estimate = PitCallCalculator.Estimate(context.State.Cars, StrategyFixtures.Focus, pitLoss, new PitCallOptions())!;

        // Foco P5 con autos cada 1,5 s detrás: con 22,125 s lo pasan 14; bajo SC (×0,5) lo pasan 7.
        Assert.Equal(5, estimate.CurrentPosition);
        Assert.Equal(14, estimate.Normal.PositionsLost);
        Assert.Equal(19, estimate.Normal.ExitPosition);
        Assert.Equal(7, estimate.SafetyCar.PositionsLost);
        Assert.Equal(12, estimate.SafetyCar.ExitPosition);
    }

    [Fact]
    public void Calculate_FewValidPitStops_DefaultPitLossFromOptions()
    {
        // 70 s no es una parada normal (drive-through, auto guardado): quedan 2 válidas de las 3 que pide la sesión.
        var context = StrategyFixtures.Race(Lap) with { PitLaneDurations = [21.5, 22.3, 70.0] };

        var snapshot = StrategyFixtures.Calculator(new PitCallOptions { DefaultPitLossSeconds = 25.0 })
            .Calculate(context, StrategyFixtures.Focus)!.Snapshot;

        Assert.Equal(25.0, snapshot.PitLossSeconds);
    }

    [Fact]
    public void Calculate_EnergyEstimate_UsedForSnapshot()
    {
        var estimate = new EnergyEstimate(SocPct: 15, HarvestedLapMj: 1.2, DeployedLapMj: 1.5, OvertakeAvailable: false, IsEstimated: true);
        var context = StrategyFixtures.Race(Lap) with { Energy = new Dictionary<int, EnergyEstimate> { [StrategyFixtures.Focus] = estimate } };

        var snapshot = StrategyFixtures.Calculator().Calculate(context, StrategyFixtures.Focus)!.Snapshot;

        Assert.Equal(estimate, snapshot.Energy);
    }

    [Fact]
    public void Calculate_CompoundWithoutModel_FallbackFromRecentPace()
    {
        var race = StrategyFixtures.Race(Lap);
        var context = race with
        {
            State = race.State with
            {
                Cars = [.. race.State.Cars.Select(car => car.DriverNumber == StrategyFixtures.Focus ? car with { Compound = Compound.Wet } : car)],
            },
        };

        var tyre = StrategyFixtures.Calculator().Calculate(context, StrategyFixtures.Focus)!.Snapshot.Tyre;

        Assert.True(tyre.IsFallback);
        Assert.Equal(Compound.Wet, tyre.Compound);
        Assert.Equal(0, tyre.DegPerLap);
        Assert.Equal(TyreDegradationModel.NoCliff, tyre.LapsToCliff);
        // Mediana de sus últimas 5 vueltas sin combustible: base 72,25 + 0,06 · edad (6..10).
        Assert.InRange(tyre.BaseLapTime, 72.5, 73.0);
    }

    [Fact]
    public void Calculate_DriverNotInRace_ReturnsNull()
    {
        var result = StrategyFixtures.Calculator().Calculate(StrategyFixtures.Race(Lap), driverNumber: 99);

        Assert.Null(result);
    }
}
