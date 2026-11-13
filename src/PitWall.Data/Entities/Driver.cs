namespace PitWall.Data.Entities;

public sealed class Driver
{
    public int SessionKey { get; set; }

    public int DriverNumber { get; set; }

    public required string FullName { get; set; }

    public required string Acronym { get; set; }

    public required string Team { get; set; }

    public required string TeamColour { get; set; }
}
