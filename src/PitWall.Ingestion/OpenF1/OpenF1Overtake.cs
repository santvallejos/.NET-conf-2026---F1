namespace PitWall.Ingestion.OpenF1;

public sealed record OpenF1Overtake
{
    public DateTimeOffset Date { get; init; }

    public int OvertakingDriverNumber { get; init; }

    public int OvertakenDriverNumber { get; init; }

    public int Position { get; init; }
}
