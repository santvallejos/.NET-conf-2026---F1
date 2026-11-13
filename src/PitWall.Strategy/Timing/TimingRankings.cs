namespace PitWall.Strategy.Timing;

/// <summary>
/// Rankings de la parrilla ordenados de menor a mayor tiempo (empate: menor número de piloto).
/// Solo figuran los pilotos con valor en cada ranking.
/// </summary>
public sealed record TimingRankings(
    IReadOnlyList<TimingRankEntry> BestS1,
    IReadOnlyList<TimingRankEntry> BestS2,
    IReadOnlyList<TimingRankEntry> BestS3,
    IReadOnlyList<TimingRankEntry> BestLap,
    IReadOnlyList<TimingRankEntry> IdealLap);
