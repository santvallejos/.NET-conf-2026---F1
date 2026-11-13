using System.ComponentModel.DataAnnotations;

namespace PitWall.Web.Services;

/// <summary>Sección <c>PitWall</c> de la configuración que usa la UI (docs/configuration.md §1).</summary>
public sealed class PitWallOptions
{
    public const string SectionName = "PitWall";

    /// <summary>Raíz de los mp3 de radio (<c>{RadioPath}/{session_key}/</c>); relativa a la raíz del repo.</summary>
    [Required]
    public string RadioPath { get; set; } = "data/radio";

    /// <summary>Sesión que se carga al arrancar si está ingerida; si no, se carga la primera disponible.</summary>
    [Range(1, int.MaxValue)]
    public int? DefaultSessionKey { get; set; }

    /// <summary>Piloto foco: su fila se resalta con el color de equipo.</summary>
    [Range(1, 99)]
    public int FocusDriver { get; set; } = 43;

    /// <summary>
    /// Pilotos cuya estrategia se calcula en segundo plano además del foco. Sin default en código: el binder agrega
    /// los valores de configuración a los de un array ya inicializado.
    /// </summary>
    [MaxLength(3)]
    public int[] BackgroundDrivers { get; set; } = [];
}
