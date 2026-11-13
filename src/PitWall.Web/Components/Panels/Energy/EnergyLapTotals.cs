namespace PitWall.Web.Components.Panels.Energy;

/// <summary>Energía ESTIMADA recuperada y desplegada en una vuelta completa.</summary>
public sealed record EnergyLapTotals(int Lap, double HarvestedMj, double DeployedMj);
