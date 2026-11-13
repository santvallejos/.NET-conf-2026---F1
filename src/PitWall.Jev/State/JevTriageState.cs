namespace PitWall.Jev.State;

/// <summary>
/// <c>state</c> de triage (docs/jev.md §3.1): mensajes nuevos o eventos inyectados más un resumen mínimo del auto.
/// </summary>
public sealed record JevTriageState(
    int Lap,
    int Pos,
    JevTyreState Tyre,
    JevWeatherState Weather,
    JevTrackState Track,
    IReadOnlyList<string> RaceControl,
    IReadOnlyList<string> Injected);
