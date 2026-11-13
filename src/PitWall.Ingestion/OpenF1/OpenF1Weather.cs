namespace PitWall.Ingestion.OpenF1;

public sealed record OpenF1Weather
{
    public DateTimeOffset Date { get; init; }

    public double AirTemperature { get; init; }

    public double TrackTemperature { get; init; }

    public double Humidity { get; init; }

    /// <summary>OpenF1 lo publica como entero 0/1.</summary>
    public int Rainfall { get; init; }

    public double WindSpeed { get; init; }

    public int WindDirection { get; init; }

    public double Pressure { get; init; }
}
