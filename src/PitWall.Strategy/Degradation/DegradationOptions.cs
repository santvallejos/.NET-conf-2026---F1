using System.ComponentModel.DataAnnotations;

namespace PitWall.Strategy.Degradation;

/// <summary>Parámetros del modelo de degradación; claves de la sección <c>Strategy</c> (docs/configuration.md §1).</summary>
public sealed class DegradationOptions
{
    public const string SectionName = "Strategy";

    /// <summary>Segundos por vuelta que pesa el combustible restante; se descuentan antes de la regresión.</summary>
    [Range(0.0, 1.0)]
    public double FuelCoefSecondsPerLap { get; set; } = 0.03;

    /// <summary>Pérdida respecto a goma nueva a partir de la cual el neumático entra en el cliff.</summary>
    [Range(0.01, 30.0)]
    public double CliffDeltaSeconds { get; set; } = 1.5;

    /// <summary>Vueltas limpias mínimas para ajustar el modelo propio; con menos se usa la media de la parrilla.</summary>
    [Range(3, 100)]
    public int MinCleanLaps { get; set; } = 4;
}
