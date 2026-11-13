namespace PitWall.Jev.State;

/// <summary><c>energy_est</c> del <c>state</c>: energía ESTIMADA (ADR-013).</summary>
public sealed record JevEnergyState(double SocPct, bool OvertakeAvailable);
