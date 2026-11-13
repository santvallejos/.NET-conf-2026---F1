using System.ComponentModel.DataAnnotations;

namespace PitWall.Strategy.MonteCarlo;

/// <summary>
/// Parámetros de Monte Carlo; sección <c>Strategy:MonteCarlo</c> (docs/configuration.md §1). Los priors marcados
/// ESTIMADO son supuestos de la demo, no cifras oficiales ni medidas de la sesión.
/// </summary>
public sealed class MonteCarloOptions
{
    public const string SectionName = "Strategy:MonteCarlo";

    /// <summary>Iteraciones por estrategia candidata.</summary>
    [Range(100, 1_000_000)]
    public int Iterations { get; set; } = 10_000;

    /// <summary>Semilla base; cada candidata usa <c>seed ^ candidateIndex</c>.</summary>
    public int Seed { get; set; } = 1412;

    [Range(0.0, 1.0)]
    public double ProbSafetyCarPerLap { get; set; } = 0.02;

    [Range(0.0, 1.0)]
    public double ProbVscPerLap { get; set; } = 0.02;

    /// <summary>Mejores estrategias que se envían a Jev en el <c>state</c>.</summary>
    [Range(1, 50)]
    public int TopOutcomesToJev { get; set; } = 5;

    [Range(1, 20)]
    public int SafetyCarLaps { get; set; } = 4;

    [Range(1, 20)]
    public int VscLaps { get; set; } = 2;

    /// <summary>Fracción del pit loss que se pierde parando bajo SC.</summary>
    [Range(0.0, 1.0)]
    public double SafetyCarPitLossFactor { get; set; } = 0.5;

    /// <summary>Fracción del pit loss que se pierde parando bajo VSC.</summary>
    [Range(0.0, 1.0)]
    public double VscPitLossFactor { get; set; } = 0.65;

    /// <summary>ESTIMADO: vuelta bajo SC respecto al ritmo base más rápido de la parrilla.</summary>
    [Range(1.0, 3.0)]
    public double SafetyCarLapFactor { get; set; } = 1.4;

    /// <summary>ESTIMADO: vuelta bajo VSC respecto al ritmo base más rápido de la parrilla.</summary>
    [Range(1.0, 3.0)]
    public double VscLapFactor { get; set; } = 1.3;

    /// <summary>ESTIMADO: separación máxima entre autos consecutivos al agruparse detrás del SC.</summary>
    [Range(0.1, 5.0)]
    public double SafetyCarGapSeconds { get; set; } = 0.8;

    /// <summary>Pit loss si la sesión aún no tiene <see cref="MinPitSamples"/> paradas con <c>lane_duration</c>.</summary>
    [Range(5.0, 60.0)]
    public double PitLossMeanSeconds { get; set; } = 22.0;

    [Range(0.0, 10.0)]
    public double PitLossSigmaSeconds { get; set; } = 1.5;

    [Range(1, 100)]
    public int MinPitSamples { get; set; } = 3;

    /// <summary>ESTIMADO: probabilidad por vuelta de que pare de llover (Markov seco/lluvia).</summary>
    [Range(0.0, 1.0)]
    public double RainStopProbPerLap { get; set; } = 0.1;

    /// <summary>ESTIMADO: vueltas sobre las que se reparte el "RISK OF RAIN x%" de Race Control.</summary>
    [Range(1, 100)]
    public int RainRiskHorizonLaps { get; set; } = 10;

    /// <summary>ESTIMADO: pérdida por vuelta de un compuesto seco con lluvia.</summary>
    [Range(0.0, 60.0)]
    public double DryCompoundInRainPenaltySeconds { get; set; } = 6.0;

    /// <summary>ESTIMADO: pérdida por vuelta de intermedio o lluvia con pista seca.</summary>
    [Range(0.0, 60.0)]
    public double WetCompoundInDryPenaltySeconds { get; set; } = 3.0;

    /// <summary>ESTIMADO: multiplicador de la pérdida de un compuesto seco con lluvia fuerte inyectada.</summary>
    [Range(1.0, 10.0)]
    public double HeavyRainPenaltyFactor { get; set; } = 2.0;

    /// <summary>ESTIMADO: probabilidad base de completar un adelantamiento cuando el ritmo alcanza al de adelante.</summary>
    [Range(0.0, 1.0)]
    public double OvertakeBaseProbability { get; set; } = 0.3;

    /// <summary>
    /// ESTIMADO: adelantamientos por auto y vuelta de una carrera "normal"; la base se escala por la tasa real de
    /// <c>overtakes</c> de la sesión respecto a esta.
    /// </summary>
    [Range(0.001, 1.0)]
    public double OvertakeReferenceRatePerCarLap { get; set; } = 0.03;

    /// <summary>Car-laps mínimos de la sesión para usar su tasa de adelantamientos.</summary>
    [Range(1, 10_000)]
    public int MinCarLapsForOvertakeRate { get; set; } = 100;

    /// <summary>ESTIMADO: probabilidad extra por segundo de ritmo a favor en la vuelta.</summary>
    [Range(0.0, 5.0)]
    public double OvertakePaceSlopePerSecond { get; set; } = 0.25;

    /// <summary>ESTIMADO: probabilidad extra con overtake mode disponible (reglamento 2026).</summary>
    [Range(0.0, 1.0)]
    public double OvertakeModeBonus { get; set; } = 0.15;

    /// <summary>Con más ritmo a favor que esto (o si el de adelante paró) el adelantamiento es seguro.</summary>
    [Range(0.1, 60.0)]
    public double CertainOvertakeDeltaSeconds { get; set; } = 2.0;

    [Range(0.0, 1.0)]
    public double MaxOvertakeProbability { get; set; } = 0.9;

    /// <summary>Distancia a la que queda un auto que no consigue adelantar.</summary>
    [Range(0.0, 5.0)]
    public double FollowGapSeconds { get; set; } = 0.3;

    /// <summary>Vuelta lenta con pinchazo (docs/scenarios.md: +20–40 s).</summary>
    [Range(0.0, 300.0)]
    public double PunctureLossMinSeconds { get; set; } = 20.0;

    [Range(0.0, 300.0)]
    public double PunctureLossMaxSeconds { get; set; } = 40.0;

    /// <summary>σ de ritmo de un auto sin modelo de degradación (ni propio ni de parrilla).</summary>
    [Range(0.0, 10.0)]
    public double FallbackLapSigmaSeconds { get; set; } = 0.5;
}
