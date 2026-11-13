namespace PitWall.Strategy.Degradation;

/// <summary>Vuelta del piloto y compuesto ajustados, con su tiempo corregido por combustible.</summary>
/// <param name="FuelCorrectedLapTime"><c>lap_time − fuel_coef · laps_remaining</c>; <c>null</c> sin tiempo.</param>
public sealed record DegradationPoint(
    int LapNumber,
    int TyreAge,
    double? LapTime,
    double? FuelCorrectedLapTime,
    LapExclusion Exclusion)
{
    public bool IsClean => Exclusion == LapExclusion.None;
}
