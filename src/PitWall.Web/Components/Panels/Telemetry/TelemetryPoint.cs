namespace PitWall.Web.Components.Panels.Telemetry;

/// <summary>Muestra ubicada dentro de su vuelta: distancia (m) y tiempo (s) desde el inicio de la vuelta.</summary>
public readonly record struct TelemetryPoint(double DistanceM, double ElapsedSeconds, TelemetrySample Sample);
