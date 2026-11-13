namespace PitWall.Web.Services;

/// <summary>Muestra de la tabla <c>Weather</c> con los campos que <c>WeatherState</c> no trae (viento).</summary>
public sealed record WeatherReading(
    DateTimeOffset Date,
    double AirTemp,
    double TrackTemp,
    double Humidity,
    bool Rainfall,
    double WindSpeed,
    int WindDirection);
