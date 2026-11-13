namespace PitWall.Data.Entities;

public sealed class TeamRadio
{
    public int SessionKey { get; set; }

    public int DriverNumber { get; set; }

    public DateTimeOffset Date { get; set; }

    public required string RecordingUrl { get; set; }

    public required string LocalPath { get; set; }
}
