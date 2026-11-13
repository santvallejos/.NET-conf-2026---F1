using PitWall.Domain;

namespace PitWall.Jev.State;

/// <summary><c>tyre</c> del <c>state</c>; el triage solo lleva compuesto y edad (docs/jev.md §3).</summary>
/// <param name="LapsToCliff">null si la goma no tiene cliff (no degrada).</param>
public sealed record JevTyreState(
    Compound Compound,
    int Age,
    double? DegSPerLap = null,
    int? LapsToCliff = null);
