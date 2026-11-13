using System.ComponentModel.DataAnnotations;

namespace PitWall.Strategy.Scenarios;

/// <summary>
/// Defaults y efectos de los eventos inyectados; sección <c>Scenarios</c> (docs/configuration.md §1). Son supuestos
/// de la demo, no datos oficiales: ESTIMADO.
/// </summary>
public sealed class ScenarioOptions
{
    public const string SectionName = "Scenarios";

    private const double DefaultPunctureLossMinSeconds = 20.0;
    private const double DefaultPunctureLossMaxSeconds = 40.0;

    [Range(1, 30)]
    public int SafetyCarDefaultLaps { get; set; } = 4;

    [Range(1, 30)]
    public int VscDefaultLaps { get; set; } = 2;

    /// <summary>Fracción del pit loss que se pierde parando bajo el SC inyectado.</summary>
    [Range(0.0, 1.0)]
    public double SafetyCarPitLossFactor { get; set; } = 0.5;

    /// <summary>Fracción del pit loss que se pierde parando bajo el VSC inyectado.</summary>
    [Range(0.0, 1.0)]
    public double VscPitLossFactor { get; set; } = 0.65;

    /// <summary>
    /// Vuelta lenta con pinchazo <c>[mín, máx]</c> en segundos; vacío = <c>[20, 40]</c>. Sin default en el array: el
    /// binder agregaría los valores de configuración a los del código.
    /// </summary>
    public double[] PunctureLossSeconds { get; set; } = [];

    [Range(0.0, 10.0)]
    public double WingDamageMinorSecPerLap { get; set; } = 0.3;

    [Range(0.0, 10.0)]
    public double WingDamageMajorSecPerLap { get; set; } = 1.5;

    /// <summary>ESTIMADO: duración de la lluvia inyectada si no se indica.</summary>
    [Range(1, 100)]
    public int RainDefaultLaps { get; set; } = 10;

    /// <summary>ESTIMADO: pérdida por vuelta con falla de ERS leve (despliegue reducido).</summary>
    [Range(0.0, 10.0)]
    public double ErsFailureMinorSecPerLap { get; set; } = 0.5;

    /// <summary>ESTIMADO: pérdida por vuelta con falla de ERS grave.</summary>
    [Range(0.0, 10.0)]
    public double ErsFailureMajorSecPerLap { get; set; } = 2.0;

    /// <summary>ESTIMADO: probabilidad de SC en la vuelta del choque de un rival.</summary>
    [Range(0.0, 1.0)]
    public double RivalCrashSafetyCarProbability { get; set; } = 0.3;

    public double PunctureLossMinSeconds => PunctureLossSeconds.Length == 2 ? PunctureLossSeconds[0] : DefaultPunctureLossMinSeconds;

    public double PunctureLossMaxSeconds => PunctureLossSeconds.Length == 2 ? PunctureLossSeconds[1] : DefaultPunctureLossMaxSeconds;

    /// <summary><see cref="PunctureLossSeconds"/> vacío o <c>[mín, máx]</c> con 0 ≤ mín ≤ máx ≤ 300.</summary>
    public bool HasValidPunctureLoss() =>
        PunctureLossSeconds.Length == 0
        || (PunctureLossSeconds.Length == 2 && PunctureLossSeconds[0] >= 0 && PunctureLossSeconds[0] <= PunctureLossSeconds[1] && PunctureLossSeconds[1] <= 300);
}
