namespace PitWall.Ingestion.OpenF1;

public sealed record OpenF1RaceControl
{
    public DateTimeOffset Date { get; init; }

    public int? LapNumber { get; init; }

    public string? Category { get; init; }

    public string? Flag { get; init; }

    public string? Scope { get; init; }

    public int? DriverNumber { get; init; }

    public string? Message { get; init; }
}
