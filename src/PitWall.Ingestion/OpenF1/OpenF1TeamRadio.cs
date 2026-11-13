namespace PitWall.Ingestion.OpenF1;

public sealed record OpenF1TeamRadio
{
    public int DriverNumber { get; init; }

    public DateTimeOffset Date { get; init; }

    public string? RecordingUrl { get; init; }
}
