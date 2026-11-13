using PitWall.Domain;

namespace PitWall.Jev.Requests;

/// <summary>Lo necesario para armar el triage de un piloto ante mensajes nuevos o eventos inyectados.</summary>
/// <param name="RaceControl">Textos de los mensajes nuevos de Race Control relevantes para el piloto.</param>
/// <param name="Injected">Eventos inyectados.</param>
public sealed record JevTriageInput(
    int SessionKey,
    RaceState State,
    int DriverNumber,
    IReadOnlyList<string> RaceControl,
    IReadOnlyList<string> Injected);
