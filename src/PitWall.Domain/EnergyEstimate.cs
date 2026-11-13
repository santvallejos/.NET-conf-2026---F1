namespace PitWall.Domain;

/// <summary>
/// Energía 2026 ESTIMADA desde <c>car_data</c> (docs/strategy-engine.md §2): OpenF1 no expone batería/ERS.
/// </summary>
public sealed record EnergyEstimate(
    double SocPct,
    double HarvestedLapMj,
    double DeployedLapMj,
    bool OvertakeAvailable,
    bool IsEstimated);
