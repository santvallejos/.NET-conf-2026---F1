namespace PitWall.Ingestion.OpenF1;

public sealed record OpenF1Location
{
    public int DriverNumber { get; init; }

    public DateTimeOffset Date { get; init; }

    public int X { get; init; }

    public int Y { get; init; }

    public int Z { get; init; }
}
