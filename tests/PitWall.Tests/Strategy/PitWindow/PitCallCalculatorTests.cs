using PitWall.Domain;
using PitWall.Strategy.PitWindow;

namespace PitWall.Tests.Strategy.PitWindow;

public sealed class PitCallCalculatorTests
{
    private static readonly PitCallOptions Options = new() { ScPitLossFactor = 0.5, VscPitLossFactor = 0.65 };

    [Fact]
    public void Estimate_NoCarWithinPitLoss_LosesNoPositions()
    {
        // Foco P2; el de atrás está a 25 s, más que el pit loss de 22 s.
        var cars = Field((1, null), (2, 3.0), (3, 25.0), (4, 2.0));

        var estimate = PitCallCalculator.Estimate(cars, 2, 22.0, Options)!;

        Assert.Equal(2, estimate.CurrentPosition);
        Assert.Equal(0, estimate.Normal.PositionsLost);
        Assert.Equal(2, estimate.Normal.ExitPosition);
    }

    [Fact]
    public void Estimate_CarsWithinPitLoss_PassTheFocus()
    {
        // Diferencias acumuladas detrás del foco (P1): 5, 12, 20, 30 s. Con 22 s pasan tres.
        var cars = Field((1, null), (2, 5.0), (3, 7.0), (4, 8.0), (5, 10.0));

        var estimate = PitCallCalculator.Estimate(cars, 1, 22.0, Options)!;

        Assert.Equal(3, estimate.Normal.PositionsLost);
        Assert.Equal(4, estimate.Normal.ExitPosition);
    }

    [Fact]
    public void Estimate_UnderScAndVsc_LosesLessOrEqual()
    {
        var cars = Field((1, null), (2, 5.0), (3, 7.0), (4, 8.0), (5, 10.0));

        var estimate = EstimateForLeader(cars);

        Assert.Equal(11.0, estimate.SafetyCar.PitLossSeconds, 6);
        Assert.Equal(14.3, estimate.Vsc.PitLossSeconds, 6);
        Assert.Equal(1, estimate.SafetyCar.PositionsLost);
        Assert.Equal(2, estimate.Vsc.PositionsLost);
        Assert.True(estimate.SafetyCar.PositionsLost <= estimate.Normal.PositionsLost);
        Assert.True(estimate.Vsc.PositionsLost <= estimate.Normal.PositionsLost);
    }

    [Fact]
    public void Estimate_LastCar_LosesNoPositions()
    {
        var cars = Field((1, null), (2, 1.0), (3, 1.0));

        var estimate = PitCallCalculator.Estimate(cars, 3, 22.0, Options)!;

        Assert.Equal(0, estimate.Normal.PositionsLost);
        Assert.Equal(0, estimate.SafetyCar.PositionsLost);
        Assert.Equal(0, estimate.Vsc.PositionsLost);
        Assert.Equal(3, estimate.Normal.ExitPosition);
    }

    [Fact]
    public void Estimate_CarWithoutInterval_StopsCounting()
    {
        // El P3 no tiene intervalo (doblado): no se cuenta ni él ni los de atrás.
        var cars = Field((1, null), (2, 2.0), (3, null), (4, 1.0));

        var estimate = PitCallCalculator.Estimate(cars, 1, 22.0, Options)!;

        Assert.Equal(1, estimate.Normal.PositionsLost);
    }

    [Fact]
    public void Estimate_DriverNotInField_ReturnsNull()
    {
        Assert.Null(PitCallCalculator.Estimate(Field((1, null)), 99, 22.0, Options));
    }

    private static PitCallEstimate EstimateForLeader(IReadOnlyList<CarState> cars) =>
        PitCallCalculator.Estimate(cars, 1, 22.0, Options)!;

    // Autos con número de piloto = posición e intervalo al de adelante.
    private static List<CarState> Field(params (int Position, double? Interval)[] cars) =>
        [.. cars.Select(car => new CarState(car.Position, car.Position, 10, null, car.Interval, Compound.Medium, 5, false))];
}
