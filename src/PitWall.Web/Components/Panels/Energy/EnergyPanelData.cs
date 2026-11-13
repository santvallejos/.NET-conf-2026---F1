using PitWall.Domain;

namespace PitWall.Web.Components.Panels.Energy;

/// <summary>Lo que muestra el panel Energía del piloto foco en el instante actual del replay. Todo ESTIMADO.</summary>
/// <param name="Estimate">SoC y energía de la vuelta en curso; <c>null</c> si no hay telemetría del piloto.</param>
/// <param name="SocMj">SoC en MJ (el porcentaje está en <see cref="EnergyEstimate.SocPct"/>).</param>
/// <param name="PreviousLap">Totales de la vuelta anterior completa, si la hay.</param>
/// <param name="Interval">Segundos al auto de adelante; <c>null</c> para el líder o sin dato.</param>
public sealed record EnergyPanelData(
    int DriverNumber,
    int Lap,
    EnergyEstimate? Estimate,
    double SocMj,
    double CapacityMj,
    EnergyLapTotals? PreviousLap,
    double? Interval,
    bool OvertakeDisabled,
    bool OvertakeAvailable);
