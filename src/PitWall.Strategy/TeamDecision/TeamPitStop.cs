namespace PitWall.Strategy.TeamDecision;

/// <summary>Parada real de un piloto (OpenF1 <c>pit</c>): vuelta en la que entró a boxes.</summary>
public sealed record TeamPitStop(int DriverNumber, int LapNumber);
