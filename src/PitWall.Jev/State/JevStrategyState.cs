namespace PitWall.Jev.State;

/// <summary><c>state</c> de estrategia (docs/jev.md §3.3); claves cortas para gastar menos tokens.</summary>
/// <param name="GapAhead">Segundos al auto de adelante; null si lidera o no hay dato.</param>
/// <param name="GapBehind">Segundos al auto de atrás; null si es el último o no hay dato.</param>
/// <param name="RaceControl">Textos de los mensajes recientes de Race Control.</param>
/// <param name="Injected">Eventos inyectados del escenario.</param>
/// <param name="Mc">Mejores candidatas de Monte Carlo.</param>
public sealed record JevStrategyState(
    int Lap,
    int LapsTotal,
    int Pos,
    double? GapAhead,
    double? GapBehind,
    JevTyreState Tyre,
    JevEnergyState EnergyEst,
    JevWeatherState Weather,
    JevTrackState Track,
    IReadOnlyList<string> RaceControl,
    IReadOnlyList<string> Injected,
    IReadOnlyList<JevMonteCarloEntry> Mc);
