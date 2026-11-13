using PitWall.Domain;

namespace PitWall.Strategy.TeamDecision;

/// <summary>Stint real de un piloto (OpenF1 <c>stints</c>).</summary>
public sealed record TeamStint(int DriverNumber, int StintNumber, int LapStart, int LapEnd, Compound Compound);
