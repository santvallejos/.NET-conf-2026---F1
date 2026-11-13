namespace PitWall.Data.Entities;

public sealed class PitStop
{
    public int SessionKey { get; set; }

    public int DriverNumber { get; set; }

    public int LapNumber { get; set; }

    public DateTimeOffset Date { get; set; }

    public double? PitDuration { get; set; }

    public double? LaneDuration { get; set; }

    public double? StopDuration { get; set; }
}
