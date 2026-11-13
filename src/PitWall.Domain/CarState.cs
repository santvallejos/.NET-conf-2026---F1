namespace PitWall.Domain;

/// <summary>Estado de un auto en el instante actual (docs/architecture.md §4).</summary>
public sealed record CarState(
    int DriverNumber,
    int Position,
    int Lap,
    double? GapToLeader,
    double? Interval,
    Compound Compound,
    int TyreAge,
    bool InPit);
