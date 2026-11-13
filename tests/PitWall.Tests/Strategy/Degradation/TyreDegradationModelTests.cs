using PitWall.Domain;
using PitWall.Strategy.Degradation;

namespace PitWall.Tests.Strategy.Degradation;

public sealed class TyreDegradationModelTests
{
    private const int Driver = 43;

    private readonly TyreDegradationModel _model = new(SyntheticLaps.Options());

    [Fact]
    public void Fit_SyntheticStintWithKnownDeg_RecoversDegAndBase()
    {
        var laps = SyntheticLaps.Stint(Driver, Compound.Medium, firstLap: 2, count: 20, baseLapTime: 72.0, deg: 0.08);

        var fit = _model.Fit(laps, Driver, Compound.Medium, tyreAge: 20, SyntheticLaps.TotalLaps);

        Assert.NotNull(fit);
        Assert.False(fit.Tyre.IsFallback);
        Assert.Equal(0.08, fit.Tyre.DegPerLap, 9);
        Assert.Equal(72.0, fit.Tyre.BaseLapTime, 9);
        Assert.Equal(0.0, fit.Tyre.ResidualSigma, 9);
        Assert.Equal(20, fit.CleanLaps);
        Assert.Equal(Compound.Medium, fit.Tyre.Compound);
        Assert.Equal(20, fit.Tyre.TyreAge);
    }

    [Fact]
    public void Fit_NoisySyntheticStint_RecoversDegWithinNoiseAndReportsSigma()
    {
        var laps = SyntheticLaps.Stint(Driver, Compound.Hard, firstLap: 2, count: 30, baseLapTime: 73.0, deg: 0.05, noise: 0.1);

        var fit = _model.Fit(laps, Driver, Compound.Hard, tyreAge: 30, SyntheticLaps.TotalLaps);

        Assert.NotNull(fit);
        Assert.InRange(fit.Tyre.DegPerLap, 0.045, 0.055);
        Assert.InRange(fit.Tyre.ResidualSigma, 0.03, 0.1);
        Assert.Equal(30, fit.CleanLaps);
    }

    [Fact]
    public void Fit_FuelCoefConfigured_IsRemovedBeforeRegression()
    {
        var laps = SyntheticLaps.Stint(Driver, Compound.Medium, firstLap: 2, count: 20, baseLapTime: 72.0, deg: 0.08);
        var withoutFuel = new TyreDegradationModel(SyntheticLaps.Options(fuelCoef: 0));

        var fit = withoutFuel.Fit(laps, Driver, Compound.Medium, tyreAge: 20, SyntheticLaps.TotalLaps);

        // Sin corrección, el combustible que se quema (0,03 s/vuelta) enmascara parte de la degradación.
        Assert.NotNull(fit);
        Assert.Equal(0.08 - SyntheticLaps.FuelCoef, fit.Tyre.DegPerLap, 9);
    }

    [Fact]
    public void Fit_NonCleanLaps_AreExcludedWithTheirReason()
    {
        var laps = SyntheticLaps.Stint(Driver, Compound.Soft, firstLap: 1, count: 16, baseLapTime: 71.0, deg: 0.1);
        laps[0] = laps[0] with { LapTime = 80.0 };
        laps[5] = laps[5] with { LapTime = 95.0, IsPitOutLap = true };
        laps[6] = laps[6] with { LapTime = 92.0, IsNeutralised = true };
        laps[10] = laps[10] with { LapTime = 88.0, IsPitInLap = true };
        laps[12] = laps[12] with { LapTime = null };

        var fit = _model.Fit(laps, Driver, Compound.Soft, tyreAge: 16, SyntheticLaps.TotalLaps);

        Assert.NotNull(fit);
        Assert.Equal(LapExclusion.FirstLap, ExclusionOf(fit, lap: 1));
        Assert.Equal(LapExclusion.PitOutLap, ExclusionOf(fit, lap: 6));
        Assert.Equal(LapExclusion.Neutralised, ExclusionOf(fit, lap: 7));
        Assert.Equal(LapExclusion.PitInLap, ExclusionOf(fit, lap: 11));
        Assert.Equal(LapExclusion.NoLapTime, ExclusionOf(fit, lap: 13));
        Assert.Equal(11, fit.CleanLaps);
        Assert.Equal(0.1, fit.Tyre.DegPerLap, 9);
    }

    [Fact]
    public void Fit_TrafficLapAbove3Sigma_IsExcludedAsOutlier()
    {
        var laps = SyntheticLaps.Stint(Driver, Compound.Medium, firstLap: 2, count: 15, baseLapTime: 72.0, deg: 0.07, noise: 0.05);
        laps[7] = laps[7] with { LapTime = laps[7].LapTime + 2.0 };

        var fit = _model.Fit(laps, Driver, Compound.Medium, tyreAge: 15, SyntheticLaps.TotalLaps);

        Assert.NotNull(fit);
        Assert.Equal(LapExclusion.Outlier, ExclusionOf(fit, lap: 9));
        Assert.Single(fit.Points, point => point.Exclusion == LapExclusion.Outlier);
        Assert.InRange(fit.Tyre.DegPerLap, 0.065, 0.075);
    }

    [Fact]
    public void Fit_LapsWithinNoise_AreNotOutliers()
    {
        var laps = SyntheticLaps.Stint(Driver, Compound.Medium, firstLap: 2, count: 25, baseLapTime: 72.0, deg: 0.07, noise: 0.2);

        var fit = _model.Fit(laps, Driver, Compound.Medium, tyreAge: 25, SyntheticLaps.TotalLaps);

        Assert.NotNull(fit);
        Assert.All(fit.Points, point => Assert.True(point.IsClean));
    }

    [Fact]
    public void Fit_WithLessThan4CleanLaps_UsesGridFallback()
    {
        List<LapSample> laps =
        [
            .. SyntheticLaps.Stint(Driver, Compound.Hard, firstLap: 30, count: 3, baseLapTime: 74.0, deg: 0.08),
            .. SyntheticLaps.Stint(44, Compound.Hard, firstLap: 20, count: 12, baseLapTime: 73.0, deg: 0.06),
            .. SyntheticLaps.Stint(12, Compound.Hard, firstLap: 20, count: 12, baseLapTime: 73.4, deg: 0.10),
            .. SyntheticLaps.Stint(3, Compound.Medium, firstLap: 20, count: 12, baseLapTime: 72.0, deg: 0.30),
            .. SyntheticLaps.Stint(1, Compound.Hard, firstLap: 20, count: 3, baseLapTime: 72.0, deg: 0.50),
        ];

        var fit = _model.Fit(laps, Driver, Compound.Hard, tyreAge: 3, SyntheticLaps.TotalLaps);

        // Media de 44 y 12 (mismo compuesto, >= 4 vueltas limpias); 3 (otro compuesto) y 1 (3 vueltas) no cuentan.
        Assert.NotNull(fit);
        Assert.True(fit.Tyre.IsFallback);
        Assert.Equal(2, fit.FallbackDrivers);
        Assert.Equal(0.08, fit.Tyre.DegPerLap, 9);
        Assert.Equal(74.0, fit.Tyre.BaseLapTime, 9);
        Assert.Equal(3, fit.CleanLaps);
    }

    [Fact]
    public void Fit_WithoutOwnCleanLaps_UsesGridMeanBase()
    {
        List<LapSample> laps =
        [
            .. SyntheticLaps.Stint(44, Compound.Hard, firstLap: 20, count: 12, baseLapTime: 73.0, deg: 0.06),
            .. SyntheticLaps.Stint(12, Compound.Hard, firstLap: 20, count: 12, baseLapTime: 73.4, deg: 0.10),
        ];

        var fit = _model.Fit(laps, Driver, Compound.Hard, tyreAge: 0, SyntheticLaps.TotalLaps);

        Assert.NotNull(fit);
        Assert.True(fit.Tyre.IsFallback);
        Assert.Equal(73.2, fit.Tyre.BaseLapTime, 9);
        Assert.Empty(fit.Points);
    }

    [Fact]
    public void Fit_MinCleanLapsConfigured_FallsBackBelowIt()
    {
        List<LapSample> laps =
        [
            .. SyntheticLaps.Stint(Driver, Compound.Hard, firstLap: 30, count: 5, baseLapTime: 74.0, deg: 0.08),
            .. SyntheticLaps.Stint(44, Compound.Hard, firstLap: 20, count: 12, baseLapTime: 73.0, deg: 0.06),
        ];
        var model = new TyreDegradationModel(SyntheticLaps.Options(minCleanLaps: 6));

        var fit = model.Fit(laps, Driver, Compound.Hard, tyreAge: 5, SyntheticLaps.TotalLaps);

        Assert.NotNull(fit);
        Assert.True(fit.Tyre.IsFallback);
        Assert.Equal(0.06, fit.Tyre.DegPerLap, 9);
    }

    [Fact]
    public void Fit_NoCleanLapsInGrid_ReturnsNull()
    {
        var laps = SyntheticLaps.Stint(Driver, Compound.Soft, firstLap: 2, count: 3, baseLapTime: 71.0, deg: 0.1);

        Assert.Null(_model.Fit(laps, Driver, Compound.Soft, tyreAge: 3, SyntheticLaps.TotalLaps));
    }

    [Fact]
    public void Fit_OwnModel_LapsToCliffFromCurrentTyreAge()
    {
        var laps = SyntheticLaps.Stint(Driver, Compound.Medium, firstLap: 2, count: 10, baseLapTime: 72.0, deg: 0.08);

        var fit = _model.Fit(laps, Driver, Compound.Medium, tyreAge: 10, SyntheticLaps.TotalLaps);

        // 0,08 · 19 = 1,52 > 1,5: el cliff llega con 19 vueltas de goma, 9 más que las 10 actuales.
        Assert.NotNull(fit);
        Assert.Equal(9, fit.Tyre.LapsToCliff);
    }

    [Theory]
    [InlineData(0.08, 1.5, 0, 19)]
    [InlineData(0.08, 1.5, 10, 9)]
    [InlineData(0.08, 1.0, 10, 3)]
    [InlineData(0.2, 1.5, 7, 1)]
    [InlineData(0.2, 1.5, 12, 0)]
    public void LapsToCliff_PositiveDeg_LapsUntilDeltaExceedsCliff(double deg, double cliff, int tyreAge, int expected)
    {
        var model = new TyreDegradationModel(SyntheticLaps.Options(cliff: cliff));

        Assert.Equal(expected, model.LapsToCliff(deg, tyreAge));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.02)]
    public void LapsToCliff_NonPositiveDeg_ReturnsNoCliff(double deg)
    {
        Assert.Equal(TyreDegradationModel.NoCliff, _model.LapsToCliff(deg, tyreAge: 10));
    }

    private static LapExclusion ExclusionOf(DegradationFit fit, int lap) =>
        fit.Points.Single(point => point.LapNumber == lap).Exclusion;
}
