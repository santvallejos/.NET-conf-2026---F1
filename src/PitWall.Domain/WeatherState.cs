namespace PitWall.Domain;

/// <summary>Clima en el instante actual; campos de la tabla <c>Weather</c> (docs/data-openf1.md §4).</summary>
public sealed record WeatherState(
    double AirTemp,
    double TrackTemp,
    double Humidity,
    bool Rainfall);
