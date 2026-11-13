namespace PitWall.Data.Entities;

public sealed class CarData
{
    public int SessionKey { get; set; }

    public int DriverNumber { get; set; }

    public DateTimeOffset Date { get; set; }

    public int Speed { get; set; }

    public int Rpm { get; set; }

    public int Gear { get; set; }

    public int Throttle { get; set; }

    public int Brake { get; set; }
}
