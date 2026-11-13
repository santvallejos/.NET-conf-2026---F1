using System.ComponentModel.DataAnnotations;

namespace PitWall.Jev;

/// <summary>Sección <c>Jev</c> (docs/configuration.md §1).</summary>
public sealed class JevOptions
{
    public const string SectionName = "Jev";

    public bool Enabled { get; set; } = true;

    public JevClientKind Client { get; set; } = JevClientKind.Fake;

    /// <summary>Solo por User Secrets / parámetro de Aspire (<c>Jev__ApiKey</c>), nunca en appsettings.</summary>
    public string? ApiKey { get; set; }

    [Required]
    [Url]
    public string BaseUrl { get; set; } = "https://api.typesafe.ai/v1/";

    [Required]
    public string Model { get; set; } = "jev-latest";

    public JevMode Mode { get; set; } = JevMode.EveryLap;

    // Laps y Drivers sin default en código: el binder agrega los valores de configuración a los de un array ya
    // inicializado.

    /// <summary>Vueltas en las que se consulta la estrategia con <see cref="JevMode.SpecificLaps"/>.</summary>
    public int[] Laps { get; set; } = [];

    /// <summary>Pilotos con Jev activo (on/off por piloto).</summary>
    public int[] Drivers { get; set; } = [];

    public JevTriageOptions Triage { get; set; } = new();

    public JevCacheOptions Cache { get; set; } = new();

    [Range(0, int.MaxValue)]
    public int MaxRequestsPerSession { get; set; } = 500;

    [Range(0.0, 1000.0)]
    public decimal PricePerMillionInputTokensUsd { get; set; } = 0.042m;

    [Range(1, 60_000)]
    public int TimeoutMs { get; set; } = 2000;
}
