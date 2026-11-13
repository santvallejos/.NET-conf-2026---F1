using PitWall.Strategy.Energy;

namespace PitWall.Tests.Strategy.Energy;

public sealed class EnergyModelTests
{
    private const double Capacity = 4.0;
    private const double Step = 0.25;

    private static readonly DateTimeOffset Start = new(2026, 11, 8, 17, 0, 0, TimeSpan.Zero);

    private static readonly TelemetrySample Brake = Sample(speed: 180, throttle: 0, brake: 100);
    private static readonly TelemetrySample Lift = Sample(speed: 250, throttle: 5, brake: 0);
    private static readonly TelemetrySample FullThrottle = Sample(speed: 300, throttle: 100, brake: 0);
    private static readonly TelemetrySample PartThrottle = Sample(speed: 200, throttle: 60, brake: 0);

    private readonly EnergyModel _model = new(Options());

    [Fact]
    public void Classify_BrakePressed_HarvestsEvenWithThrottle()
    {
        Assert.Equal(EnergyPhase.HarvestBrake, _model.Classify(Brake));
        Assert.Equal(EnergyPhase.HarvestBrake, _model.Classify(Sample(speed: 300, throttle: 100, brake: 100)));
    }

    [Theory]
    [InlineData(250, 5, EnergyPhase.HarvestLift)]
    [InlineData(151, 19, EnergyPhase.HarvestLift)]
    [InlineData(250, 20, EnergyPhase.None)]
    [InlineData(150, 0, EnergyPhase.None)]
    [InlineData(90, 0, EnergyPhase.None)]
    public void Classify_LiftAndCoast_OnlyBelow20ThrottleAbove150Kmh(int speed, int throttle, EnergyPhase expected) =>
        Assert.Equal(expected, _model.Classify(Sample(speed, throttle, brake: 0)));

    [Theory]
    [InlineData(300, 100, EnergyPhase.Deploy)]
    [InlineData(121, 96, EnergyPhase.Deploy)]
    [InlineData(300, 95, EnergyPhase.None)]
    [InlineData(120, 100, EnergyPhase.None)]
    [InlineData(200, 60, EnergyPhase.None)]
    public void Classify_FullThrottle_OnlyAbove95ThrottleAndSpeedDeployMin(int speed, int throttle, EnergyPhase expected) =>
        Assert.Equal(expected, _model.Classify(Sample(speed, throttle, brake: 0)));

    [Fact]
    public void Classify_SpeedDeployMinConfigured_UsesConfiguredSpeed()
    {
        var model = new EnergyModel(Options(speedDeployMin: 250));

        Assert.Equal(EnergyPhase.None, model.Classify(Sample(speed: 240, throttle: 100, brake: 0)));
        Assert.Equal(EnergyPhase.Deploy, model.Classify(Sample(speed: 260, throttle: 100, brake: 0)));
    }

    [Fact]
    public void Integrate_FullThrottle_DeploysDeployKwTimesDt()
    {
        var trace = _model.Integrate(Sequence((FullThrottle, 8), (PartThrottle, 1)));

        // 8 intervalos de 0,25 s a 350 kW = 0,7 MJ.
        Assert.Equal(0.7, trace.DeployedMj(Start, End(trace)), 9);
        Assert.Equal(0.0, trace.HarvestedMj(Start, End(trace)), 9);
        Assert.Equal(Capacity - 0.7, trace.SocMjAt(End(trace)), 9);
    }

    [Fact]
    public void Integrate_Braking_HarvestsHarvestBrakeKwTimesDt()
    {
        var model = new EnergyModel(Options(harvestBrakeKw: 200));

        var trace = model.Integrate(Sequence((FullThrottle, 4), (Brake, 4), (PartThrottle, 1)));

        // Despliega 4 · 0,25 s · 350 kW = 0,35 MJ y recupera 4 · 0,25 s · 200 kW = 0,2 MJ.
        Assert.Equal(0.35, trace.DeployedMj(Start, End(trace)), 9);
        Assert.Equal(0.2, trace.HarvestedMj(Start, End(trace)), 9);
        Assert.Equal(Capacity - 0.35 + 0.2, trace.SocMjAt(End(trace)), 9);
    }

    [Fact]
    public void Integrate_LiftAndCoast_HarvestsHarvestLiftKwTimesDt()
    {
        var trace = _model.Integrate(Sequence((FullThrottle, 8), (Lift, 4), (PartThrottle, 1)));

        // 4 · 0,25 s · 120 kW = 0,12 MJ.
        Assert.Equal(0.12, trace.HarvestedMj(Start, End(trace)), 9);
        Assert.Equal(Capacity - 0.7 + 0.12, trace.SocMjAt(End(trace)), 9);
    }

    [Fact]
    public void Integrate_PartThrottle_NeitherHarvestsNorDeploys()
    {
        var trace = _model.Integrate(Sequence((FullThrottle, 4), (PartThrottle, 10)));

        Assert.Equal(0.35, trace.DeployedMj(Start, End(trace)), 9);
        Assert.Equal(0.0, trace.HarvestedMj(Start, End(trace)), 9);
    }

    [Fact]
    public void Integrate_LongDeploy_SocStopsAtZero()
    {
        // 200 intervalos · 0,0875 MJ = 17,5 MJ pedidos con 4 MJ en batería.
        var trace = _model.Integrate(Sequence((FullThrottle, 200)));

        Assert.Equal(0.0, trace.SocMjAt(End(trace)), 9);
        Assert.Equal(Capacity, trace.DeployedMj(Start, End(trace)), 9);
    }

    [Fact]
    public void Integrate_HarvestWithFullBattery_SocStopsAtCapacity()
    {
        var trace = _model.Integrate(Sequence((Brake, 50), (Lift, 50)));

        Assert.Equal(Capacity, trace.SocMjAt(End(trace)), 9);
        Assert.Equal(0.0, trace.HarvestedMj(Start, End(trace)), 9);
    }

    [Fact]
    public void Integrate_RandomTelemetry_SocAlwaysWithinZeroAndCapacity()
    {
        var random = new Random(1412);
        var samples = Enumerable.Range(0, 5000)
            .Select(i => new TelemetrySample(
                Start.AddSeconds(i * Step),
                random.Next(60, 340),
                random.Next(0, 2) == 0 ? random.Next(0, 101) : 100,
                random.Next(0, 4) == 0 ? 100 : 0))
            .ToList();

        var trace = _model.Integrate(samples);

        foreach (var sample in samples)
        {
            Assert.InRange(trace.SocMjAt(sample.Date), 0.0, Capacity);
        }

        Assert.Equal(
            Capacity + trace.HarvestedMj(Start, End(trace)) - trace.DeployedMj(Start, End(trace)),
            trace.SocMjAt(End(trace)),
            9);
    }

    [Fact]
    public void Integrate_GapLongerThanMax_IsNotIntegrated()
    {
        TelemetrySample[] samples =
        [
            FullThrottle with { Date = Start },
            FullThrottle with { Date = Start.AddSeconds(EnergyModel.MaxSampleGapSeconds + 1) },
            PartThrottle with { Date = Start.AddSeconds(EnergyModel.MaxSampleGapSeconds + 1 + Step) },
        ];

        var trace = _model.Integrate(samples);

        Assert.Equal(0.0875, trace.DeployedMj(Start, samples[^1].Date), 9);
    }

    [Fact]
    public void Integrate_UnorderedSamples_IntegratesInDateOrder()
    {
        var samples = Sequence((FullThrottle, 4), (PartThrottle, 1));
        samples.Reverse();

        var trace = _model.Integrate(samples);

        Assert.Equal(0.35, trace.DeployedMj(Start, End(trace)), 9);
    }

    [Fact]
    public void Integrate_NoSamples_KeepsFullBattery()
    {
        var trace = _model.Integrate([]);

        Assert.Equal(0, trace.Count);
        Assert.Equal(Capacity, trace.SocMjAt(Start), 9);
    }

    [Fact]
    public void Estimate_LapWindow_CountsOnlyEnergySinceLapStart()
    {
        // Vuelta anterior: despliega 0,7 MJ; vuelta actual (desde la muestra 8): recupera 0,35 MJ y despliega 0,175.
        var trace = _model.Integrate(Sequence((FullThrottle, 8), (Brake, 4), (FullThrottle, 2), (PartThrottle, 1)));
        var lapStart = Start.AddSeconds(8 * Step);

        var estimate = EnergyModel.Estimate(trace, lapStart, End(trace), interval: null, overtakeDisabled: false);

        Assert.Equal(0.35, estimate.HarvestedLapMj, 9);
        Assert.Equal(0.175, estimate.DeployedLapMj, 9);
        Assert.Equal((Capacity - 0.7 + 0.35 - 0.175) / Capacity * 100.0, estimate.SocPct, 9);
    }

    [Fact]
    public void Estimate_ClockMidTrace_IgnoresLaterSamples()
    {
        var trace = _model.Integrate(Sequence((FullThrottle, 8), (PartThrottle, 1)));

        var estimate = EnergyModel.Estimate(trace, Start, Start.AddSeconds(4 * Step), interval: null, overtakeDisabled: false);

        Assert.Equal(0.35, estimate.DeployedLapMj, 9);
        Assert.Equal((Capacity - 0.35) / Capacity * 100.0, estimate.SocPct, 9);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(0.5, true)]
    [InlineData(1.5, false)]
    public void Estimate_AnyInput_IsEstimated(double? interval, bool overtakeDisabled)
    {
        var trace = _model.Integrate(Sequence((FullThrottle, 4), (PartThrottle, 1)));

        var estimate = EnergyModel.Estimate(trace, Start, End(trace), interval, overtakeDisabled);

        Assert.True(estimate.IsEstimated);
    }

    [Theory]
    [InlineData(0.2, false, true)]
    [InlineData(0.999, false, true)]
    [InlineData(1.0, false, false)]
    [InlineData(2.5, false, false)]
    [InlineData(null, false, false)]
    [InlineData(0.2, true, false)]
    public void IsOvertakeAvailable_IntervalAndRaceControl_OnlyBelowOneSecondAndNotDisabled(double? interval, bool overtakeDisabled, bool expected)
    {
        Assert.Equal(expected, EnergyModel.IsOvertakeAvailable(interval, overtakeDisabled));

        var trace = _model.Integrate([]);
        Assert.Equal(expected, EnergyModel.Estimate(trace, Start, Start, interval, overtakeDisabled).OvertakeAvailable);
    }

    [Fact]
    public void Constructor_NonPositiveCapacity_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new EnergyModel(new EnergyOptions { BatteryCapacityMj = 0 }));

    private static EnergyOptions Options(double harvestBrakeKw = 350, int speedDeployMin = 120) => new()
    {
        BatteryCapacityMj = Capacity,
        HarvestBrakeKw = harvestBrakeKw,
        HarvestLiftKw = 120,
        DeployKw = 350,
        SpeedDeployMin = speedDeployMin,
    };

    private static TelemetrySample Sample(int speed, int throttle, int brake) => new(Start, speed, throttle, brake);

    // Muestras cada 0,25 s desde Start: cada bloque repite su muestra el número de veces indicado.
    private static List<TelemetrySample> Sequence(params (TelemetrySample Sample, int Count)[] blocks) =>
        [.. blocks
            .SelectMany(block => Enumerable.Repeat(block.Sample, block.Count))
            .Select((sample, i) => sample with { Date = Start.AddSeconds(i * Step) })];

    private static DateTimeOffset End(EnergyTrace trace) => Start.AddSeconds((trace.Count - 1) * Step);
}
