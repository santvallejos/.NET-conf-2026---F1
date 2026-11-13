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
    int Seed);
