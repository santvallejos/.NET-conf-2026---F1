namespace PitWall.Data.Entities;

public sealed class RaceControl
{
    public int Id { get; set; }

    public int SessionKey { get; set; }

    public DateTimeOffset Date { get; set; }

    public int? LapNumber { get; set; }

    public required string Category { get; set; }

    public string? Flag { get; set; }

    public string? Scope { get; set; }

    public int? DriverNumber { get; set; }

    public required string Message { get; set; }
}
