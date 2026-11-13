using PitWall.Domain;
using PitWall.Strategy.Rules;

namespace PitWall.Tests.Strategy.Rules;

/// <summary>Features neutros: bandera verde, pista seca, gomas sanas, regla de compuestos cumplida.</summary>
internal static class RuleFeaturesFactory
{
    public static RuleFeatures Neutral() => new(
        Track: new TrackStatus("GREEN", SafetyCar: false, VirtualSafetyCar: false),
        PitLaneClosed: false,
        Rain: RainIntensity.None,
        PreviousRain: RainIntensity.None,
        RainForecast: false,
        TrackWet: false,
        UsedCompounds: [Compound.Medium, Compound.Hard],
        Tyre: new TyreModel(Compound.Hard, TyreAge: 5, BaseLapTime: 72.0, DegPerLap: 0.05, ResidualSigma: 0.2, LapsToCliff: 20, IsFallback: false),
        Energy: new EnergyEstimate(SocPct: 60, HarvestedLapMj: 1.0, DeployedLapMj: 1.0, OvertakeAvailable: false, IsEstimated: true),
        GapBehind: null,
        RivalAheadPitted: false,
        ExitGap: null,
        Puncture: false,
        WingDamage: DamageSeverity.None,
        RivalIncident: false,
        ErsFailure: false);
}
