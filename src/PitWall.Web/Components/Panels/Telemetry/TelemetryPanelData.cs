namespace PitWall.Web.Components.Panels.Telemetry;

/// <summary>
/// Panel Telemetría del piloto foco en el instante del replay: última muestra, vuelta en curso hasta el reloj y
/// mejor vuelta completada hasta el reloj. <see cref="DeltaToBest"/> compara ambas a igual distancia recorrida.
/// </summary>
public sealed record TelemetryPanelData(
    int DriverNumber,
    TelemetrySample? Now,
    TelemetryLap Current,
    TelemetryLap? Best,
    double? DeltaToBest);
