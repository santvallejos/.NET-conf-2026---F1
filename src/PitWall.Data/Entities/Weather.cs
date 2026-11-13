namespace PitWall.Data.Entities;

public sealed class Weather
{
    public int SessionKey { get; set; }

    public DateTimeOffset Date { get; set; }

    public double AirTemp { get; set; }

    public double TrackTemp { get; set; }

    public double Humidity { get; set; }

    public bool Rainfall { get; set; }

    public double WindSpeed { get; set; }

    public int WindDirection { get; set; }

    public double Pressure { get; set; }
}
