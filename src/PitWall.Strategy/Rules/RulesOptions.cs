using System.ComponentModel.DataAnnotations;

namespace PitWall.Strategy.Rules;

/// <summary>Umbrales para generar razones; sección <c>Strategy:Rules</c>.</summary>
public sealed class RulesOptions
{
    public const string SectionName = "Strategy:Rules";

    [Range(0.0, 10.0)]
    public double DegHighSecondsPerLap { get; set; } = 0.10;

    [Range(0, 100)]
    public int CliffNearLaps { get; set; } = 3;

    [Range(0.0, 60.0)]
    public double UndercutGapSeconds { get; set; } = 3.0;

    [Range(0.0, 60.0)]
    public double TrafficOnExitGapSeconds { get; set; } = 2.0;

    [Range(0.0, 100.0)]
    public double EnergyLowSocPct { get; set; } = 20.0;
}
