using PitWall.Domain;

namespace PitWall.Strategy.TeamDecision;

/// <summary>Decisión real del equipo en una vuelta; solo pit y compuesto, porque ritmo y energía reales no son observables.</summary>
/// <param name="Compound">Compuesto montado en la parada; <c>null</c> en <see cref="PitCall.StayOut"/> o si no hay stint posterior.</param>
public sealed record TeamLapDecision(int DriverNumber, int Lap, PitCall PitCall, Compound? Compound);
