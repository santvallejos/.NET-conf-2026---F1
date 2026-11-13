namespace PitWall.Ingestion.OpenF1;

public sealed record OpenF1Position
{
    public int DriverNumber { get; init; }

    public DateTimeOffset Date { get; init; }

    public int Position { get; init; }
}
