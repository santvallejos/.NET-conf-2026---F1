namespace PitWall.Jev.State;

/// <summary><c>weather</c> del <c>state</c>.</summary>
/// <param name="TrackC">Temperatura de pista en °C.</param>
public sealed record JevWeatherState(bool Rain, double TrackC, double Humidity);
