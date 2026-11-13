using PitWall.Domain;
using PitWall.Strategy.Rules;

namespace PitWall.Tests.Strategy.Rules;

public sealed class RulesEngineReasonTests
{
    private readonly RulesEngine _engine = new(new RulesOptions());

    public static TheoryData<Reason> AllReasons => new(Enum.GetValues<Reason>());

    [Fact]
    public void EvaluateReasons_NeutralFeatures_ReturnsNoReasons()
    {
        Assert.Empty(_engine.EvaluateReasons(RuleFeaturesFactory.Neutral()));
    }

    [Theory]
    [MemberData(nameof(AllReasons))]
    public void EvaluateReasons_FeatureTriggeringReason_ContainsReason(Reason reason)
    {
        var reasons = _engine.EvaluateReasons(Trigger(reason));

        Assert.Contains(reason, reasons);
    }

    [Fact]
    public void EvaluateReasons_ExitGapAboveThreshold_ClearAirInsteadOfTraffic()
    {
        var reasons = _engine.EvaluateReasons(RuleFeaturesFactory.Neutral() with { ExitGap = 2.5 });

        Assert.Contains(Reason.ClearAirOnExit, reasons);
        Assert.DoesNotContain(Reason.TrafficOnExit, reasons);
    }

    [Fact]
    public void EvaluateReasons_OvertakeAvailableWithErsFailure_OmitsOvertakeAvailable()
    {
        var features = RuleFeaturesFactory.Neutral() with
        {
            Energy = RuleFeaturesFactory.Neutral().Energy with { OvertakeAvailable = true },
            ErsFailure = true,
        };

        var reasons = _engine.EvaluateReasons(features);

        Assert.DoesNotContain(Reason.OvertakeAvailable, reasons);
        Assert.Contains(Reason.ErsFailure, reasons);
    }

    private static RuleFeatures Trigger(Reason reason)
    {
        var neutral = RuleFeaturesFactory.Neutral();
        return reason switch
        {
            Reason.TyreDegHigh => neutral with { Tyre = neutral.Tyre with { DegPerLap = 0.15 } },
            Reason.TyreCliffNear => neutral with { Tyre = neutral.Tyre with { LapsToCliff = 2 } },
            Reason.UndercutThreat => neutral with { GapBehind = 1.8 },
            Reason.OvercutWindow => neutral with { RivalAheadPitted = true },
            Reason.ScDeployed => neutral with { Track = new TrackStatus("YELLOW", SafetyCar: true, VirtualSafetyCar: false) },
            Reason.VscDeployed => neutral with { Track = new TrackStatus("YELLOW", SafetyCar: false, VirtualSafetyCar: true) },
            Reason.RedFlag => neutral with { Track = new TrackStatus("RED", false, false) },
            Reason.RainStarting => neutral with { Rain = RainIntensity.Light },
            Reason.RainStopping => neutral with { PreviousRain = RainIntensity.Light, TrackWet = true },
            Reason.TrackDrying => neutral with { TrackWet = true, Tyre = neutral.Tyre with { Compound = Compound.Intermediate } },
            Reason.LowGrip => neutral with { TrackWet = true },
            Reason.TrafficOnExit => neutral with { ExitGap = 0.8 },
            Reason.ClearAirOnExit => neutral with { ExitGap = 4.0 },
            Reason.EnergyLow => neutral with { Energy = neutral.Energy with { SocPct = 10 } },
            Reason.OvertakeAvailable => neutral with { Energy = neutral.Energy with { OvertakeAvailable = true } },
            Reason.Puncture => neutral with { Puncture = true },
            Reason.WingDamage => neutral with { WingDamage = DamageSeverity.Minor },
            Reason.RivalIncident => neutral with { RivalIncident = true },
            Reason.ErsFailure => neutral with { ErsFailure = true },
            Reason.MandatoryCompoundPending => neutral with { UsedCompounds = [Compound.Hard] },
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Reason sin caso de prueba."),
        };
    }
}
