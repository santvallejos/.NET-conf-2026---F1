using System.ComponentModel.DataAnnotations;

namespace PitWall.Strategy.Energy;

/// <summary>
/// Parámetros del modelo de energía; sección <c>Strategy:Energy</c> (docs/configuration.md §1).
/// ESTIMADO: valores orientativos, no son cifras oficiales del reglamento 2026 (ADR-013).
/// </summary>
public sealed class EnergyOptions
{
    public const string SectionName = "Strategy:Energy";

    [Range(0.1, 100.0)]
    public double BatteryCapacityMj { get; set; } = 4.0;

    /// <summary>Potencia recuperada con el pedal de freno pisado.</summary>
    [Range(0.0, 2000.0)]
    public double HarvestBrakeKw { get; set; } = 350;

    /// <summary>Potencia recuperada en lift &amp; coast (acelerador suelto a alta velocidad).</summary>
    [Range(0.0, 2000.0)]
    public double HarvestLiftKw { get; set; } = 120;

    /// <summary>Potencia desplegada a fondo.</summary>
    [Range(0.0, 2000.0)]
    public double DeployKw { get; set; } = 350;

    /// <summary>Velocidad (km/h) a partir de la cual se despliega a fondo.</summary>
    [Range(0, 400)]
    public int SpeedDeployMin { get; set; } = 120;
}
