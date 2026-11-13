using PitWall.Domain;

namespace PitWall.Strategy.Rules;

/// <summary>Features de un piloto en una vuelta a partir de los que se evalúan restricciones y razones.</summary>
/// <param name="Track">Estado de pista actual (bandera, SC, VSC).</param>
/// <param name="PitLaneClosed">Pit lane cerrado según Race Control (ver <see cref="RaceControlSignals"/>).</param>
/// <param name="Rain">Intensidad de lluvia en la vuelta actual.</param>
/// <param name="PreviousRain">Intensidad de lluvia en la vuelta anterior.</param>
/// <param name="RainForecast">Hay lluvia prevista (p. ej. "RISK OF RAIN" de Race Control).</param>
/// <param name="TrackWet">La pista sigue mojada.</param>
/// <param name="UsedCompounds">Compuestos usados en la carrera hasta ahora.</param>
/// <param name="Tyre">Modelo de degradación del stint actual.</param>
/// <param name="Energy">Energía estimada del auto.</param>
/// <param name="GapBehind">Intervalo en segundos con el auto de atrás, o null si no hay.</param>
/// <param name="RivalAheadPitted">El auto de adelante acaba de entrar a boxes.</param>
/// <param name="ExitGap">Gap proyectado en segundos al auto más cercano al salir de boxes, o null si no se calculó.</param>
/// <param name="Puncture">Pinchazo del auto.</param>
/// <param name="WingDamage">Daño de alerón del auto.</param>
/// <param name="RivalIncident">Incidente de un rival cercano.</param>
/// <param name="ErsFailure">Falla de ERS del auto.</param>
public sealed record RuleFeatures(
    TrackStatus Track,
    bool PitLaneClosed,
    RainIntensity Rain,
    RainIntensity PreviousRain,
    bool RainForecast,
    bool TrackWet,
    IReadOnlyList<Compound> UsedCompounds,
    TyreModel Tyre,
    EnergyEstimate Energy,
    double? GapBehind,
    bool RivalAheadPitted,
    double? ExitGap,
    bool Puncture,
    DamageSeverity WingDamage,
    bool RivalIncident,
    bool ErsFailure);
