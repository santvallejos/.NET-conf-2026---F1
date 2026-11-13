namespace PitWall.Strategy.Energy;

/// <summary>Muestra de <c>car_data</c> (~3,7 Hz) con lo que usa el modelo de energía.</summary>
/// <param name="Speed">km/h.</param>
/// <param name="Throttle">Acelerador, 0–100 %.</param>
/// <param name="Brake">Freno; OpenF1 lo da como 0 o 100.</param>
public sealed record TelemetrySample(DateTimeOffset Date, int Speed, int Throttle, int Brake);
