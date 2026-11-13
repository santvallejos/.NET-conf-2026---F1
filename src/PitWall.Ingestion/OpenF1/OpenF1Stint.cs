namespace PitWall.Ingestion.OpenF1;

public sealed record OpenF1Stint
{
    public int DriverNumber { get; init; }

    public int StintNumber { get; init; }

    public int LapStart { get; init; }

    public int? LapEnd { get; init; }

    public string? Compound { get; init; }

    public int? TyreAgeAtStart { get; init; }
}
