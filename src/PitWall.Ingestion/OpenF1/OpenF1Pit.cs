namespace PitWall.Ingestion.OpenF1;

public sealed record OpenF1Pit
{
    public int DriverNumber { get; init; }

    public int LapNumber { get; init; }

    public DateTimeOffset Date { get; init; }

    public double? PitDuration { get; init; }

    public double? LaneDuration { get; init; }

    public double? StopDuration { get; init; }
}
