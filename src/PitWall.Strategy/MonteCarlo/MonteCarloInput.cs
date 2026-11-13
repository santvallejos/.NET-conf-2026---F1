using PitWall.Domain;

namespace PitWall.Strategy.MonteCarlo;

/// <summary>Estado desde el que Monte Carlo proyecta el resto de la carrera del piloto foco.</summary>
/// <param name="Lap">Vuelta en curso <c>L</c>; se simulan <c>L..TotalLaps</c>.</param>
/// <param name="Cars">Toda la parrilla en carrera, incluido el foco, en orden de pista.</param>
/// <param name="FocusCompounds">Modelo con goma nueva del foco por compuesto, para el stint posterior a la parada.</param>
/// <param name="Raining">Llueve al empezar la proyección.</param>
/// <param name="RainRisk">Probabilidad de lluvia anunciada por Race Control ("RISK OF RAIN x%"), 0..1.</param>
/// <param name="ActiveNeutralisation">SC o VSC desplegado al empezar la proyección.</param>
/// <param name="PitLossMean">Media de <c>lane_duration</c> de la sesión (s).</param>
/// <param name="PitLossSigma">Desvío de <c>lane_duration</c> de la sesión (s).</param>
/// <param name="OvertakeBaseProbability">Probabilidad base de adelantar, ya escalada por la sesión.</param>
/// <param name="OvertakeModeEnabled">Overtake mode no deshabilitado por Race Control ni por falla de ERS.</param>
/// <param name="FuelCoefSecondsPerLap">Peso del combustible restante en el tiempo de vuelta.</param>
/// <param name="Puncture">Evento inyectado: el foco tiene un pinchazo hasta que pare.</param>
/// <param name="Seed">Semilla de esta proyección; cada candidata deriva la suya como <c>Seed ^ candidateIndex</c>.</param>
public sealed record MonteCarloInput(
    int FocusDriver,
    int Lap,
    int TotalLaps,
    IReadOnlyList<MonteCarloCar> Cars,
    IReadOnlyDictionary<Compound, TyreModel> FocusCompounds,
    bool Raining,
    double RainRisk,
    Neutralisation ActiveNeutralisation,
    double PitLossMean,
    double PitLossSigma,
    double OvertakeBaseProbability,
    bool OvertakeModeEnabled,
    double FuelCoefSecondsPerLap,
    bool Puncture,
    int Seed)
{
    private static readonly IReadOnlyDictionary<int, double> NoPenalties = new Dictionary<int, double>();

    /// <summary>Vueltas que le quedan a <see cref="ActiveNeutralisation"/> contando la actual; null = duración por defecto.</summary>
    public int? ActiveNeutralisationLaps { get; init; }

    /// <summary>Fracción del pit loss bajo <see cref="ActiveNeutralisation"/>; null = la de <see cref="MonteCarloOptions"/>.</summary>
    public double? ActiveNeutralisationPitLossFactor { get; init; }

    /// <summary>Evento inyectado: el SC acaba de salir y agrupa la parrilla al empezar la proyección.</summary>
    public bool CompressGapsAtStart { get; init; }

    /// <summary>
    /// Evento inyectado: bandera roja en <see cref="Lap"/>. Vuelta neutralizada sin paradas, cambio de gomas libre para
    /// todos (goma nueva del mismo compuesto) y gaps reseteados a la reanudación.
    /// </summary>
    public bool RedFlag { get; init; }

    /// <summary>Evento inyectado: vueltas de lluvia cierta desde <see cref="Lap"/>; después sigue el Markov.</summary>
    public int ForcedRainLaps { get; init; }

    /// <summary>Evento inyectado: la lluvia cierta es fuerte (los secos pierden más).</summary>
    public bool HeavyRain { get; init; }

    /// <summary>Evento inyectado: probabilidad de SC en <see cref="Lap"/> (choque de un rival); null = el prior.</summary>
    public double? SafetyCarProbabilityFirstLap { get; init; }

    /// <summary>Evento inyectado: segundos por vuelta que pierde cada auto hasta su próxima parada (alerón dañado).</summary>
    public IReadOnlyDictionary<int, double> RepairablePacePenalties { get; init; } = NoPenalties;

    /// <summary>Evento inyectado: segundos por vuelta que pierde cada auto hasta el final (falla de ERS).</summary>
    public IReadOnlyDictionary<int, double> PermanentPacePenalties { get; init; } = NoPenalties;

    /// <summary>Vuelta lenta con pinchazo (s); null = la de <see cref="MonteCarloOptions"/>.</summary>
    public double? PunctureLossMinSeconds { get; init; }

    public double? PunctureLossMaxSeconds { get; init; }
}
