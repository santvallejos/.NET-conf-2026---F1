namespace PitWall.Ingestion.OpenF1;

public sealed record OpenF1Driver
{
    public int DriverNumber { get; init; }

    public string? FullName { get; init; }

    public string? NameAcronym { get; init; }

    public string? TeamName { get; init; }

    public string? TeamColour { get; init; }
}
