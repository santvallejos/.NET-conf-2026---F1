namespace PitWall.Data.Entities;

public sealed class SessionResult
{
    public int SessionKey { get; set; }

    public int DriverNumber { get; set; }

    public int? Position { get; set; }

    public int Laps { get; set; }

    public bool Dnf { get; set; }

    public bool Dns { get; set; }

    public bool Dsq { get; set; }

    public double? Duration { get; set; }

    public double? GapToLeader { get; set; }
}
