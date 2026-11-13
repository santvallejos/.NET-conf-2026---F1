namespace PitWall.Data.Entities;

public sealed class Location
{
    public int SessionKey { get; set; }

    public int DriverNumber { get; set; }

    public DateTimeOffset Date { get; set; }

    public int X { get; set; }

    public int Y { get; set; }

    public int Z { get; set; }
}
