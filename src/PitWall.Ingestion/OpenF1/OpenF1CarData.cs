namespace PitWall.Ingestion.OpenF1;

public sealed record OpenF1CarData
{
    public int DriverNumber { get; init; }

    public DateTimeOffset Date { get; init; }

    public int? Speed { get; init; }

    public int? Rpm { get; init; }

    public int? NGear { get; init; }

    public int? Throttle { get; init; }

    public int? Brake { get; init; }
}
