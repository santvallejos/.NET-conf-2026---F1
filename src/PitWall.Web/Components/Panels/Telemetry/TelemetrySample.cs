namespace PitWall.Web.Components.Panels.Telemetry;

/// <summary>Muestra de <c>car_data</c>: velocidad en km/h, acelerador y freno en %, marcha y RPM.</summary>
public readonly record struct TelemetrySample(DateTimeOffset Date, int Speed, int Throttle, int Brake, int Gear, int Rpm);
