using PitWall.Domain;

namespace PitWall.Strategy.Degradation;

/// <summary>Modelo ajustado y las vueltas del piloto con el compuesto, limpias o con su motivo de exclusión.</summary>
/// <param name="FallbackDrivers">Pilotos de la parrilla promediados si <see cref="TyreModel.IsFallback"/>; 0 si no.</param>
public sealed record DegradationFit(
    TyreModel Tyre,
    IReadOnlyList<DegradationPoint> Points,
    int FallbackDrivers)
{
    public int CleanLaps => Points.Count(point => point.IsClean);
}
