namespace PitWall.Data.Entities;

public sealed class Lap
{
    public int SessionKey { get; set; }

    public int DriverNumber { get; set; }

    public int LapNumber { get; set; }

    public DateTimeOffset? DateStart { get; set; }

    /// <summary>Segundos; <c>null</c> en la vuelta 1.</summary>
    public double? Duration { get; set; }

    public double? S1 { get; set; }

    public double? S2 { get; set; }

    public double? S3 { get; set; }

    public int? I1Speed { get; set; }

    public int? I2Speed { get; set; }

    public bool IsPitOutLap { get; set; }
}
