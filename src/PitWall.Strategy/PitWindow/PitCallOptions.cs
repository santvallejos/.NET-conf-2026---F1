using System.ComponentModel.DataAnnotations;

namespace PitWall.Strategy.PitWindow;

/// <summary>
/// Pit loss de la llamada a boxes; sección <c>Strategy:PitCall</c> (docs/configuration.md §1.1,
/// docs/strategy-engine.md §4). ESTIMADO: supuestos de la demo.
/// </summary>
public sealed class PitCallOptions
{
    public const string SectionName = "Strategy:PitCall";

    /// <summary>Pit loss si la sesión aún no tiene <see cref="MinPitSamples"/> paradas con <c>lane_duration</c>.</summary>
    [Range(5.0, 60.0)]
    public double DefaultPitLossSeconds { get; set; } = 22.0;

    /// <summary>Paradas con <c>lane_duration</c> necesarias para usar el pit loss medio de la sesión.</summary>
    [Range(1, 100)]
    public int MinPitSamples { get; set; } = 3;

    /// <summary>Fracción del pit loss normal que se pierde parando bajo Safety Car.</summary>
    [Range(0.0, 1.0)]
    public double ScPitLossFactor { get; set; } = 0.5;

    /// <summary>Fracción del pit loss normal que se pierde parando bajo VSC.</summary>
    [Range(0.0, 1.0)]
    public double VscPitLossFactor { get; set; } = 0.65;
}
