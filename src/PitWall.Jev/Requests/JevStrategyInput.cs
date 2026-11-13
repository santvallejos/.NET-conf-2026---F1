using PitWall.Domain;

namespace PitWall.Jev.Requests;

/// <summary>Lo necesario para armar la consulta de estrategia de un piloto.</summary>
/// <param name="State">Estado de carrera del instante en que se calculó <paramref name="Snapshot"/>.</param>
/// <param name="Snapshot">Estrategia calculada (degradación, energía, reglas, Monte Carlo) del piloto.</param>
/// <param name="RaceControl">Textos de los mensajes recientes de Race Control relevantes para el piloto.</param>
/// <param name="Injected">Eventos inyectados del escenario.</param>
/// <param name="TopOutcomes">Candidatas de Monte Carlo que viajan en el state (<c>Strategy:MonteCarlo:TopOutcomesToJev</c>).</param>
public sealed record JevStrategyInput(
    int SessionKey,
    RaceState State,
    StrategySnapshot Snapshot,
    int LapsTotal,
    IReadOnlyList<string> RaceControl,
    IReadOnlyList<string> Injected,
    int TopOutcomes);
