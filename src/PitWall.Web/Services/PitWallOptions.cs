using System.ComponentModel.DataAnnotations;

namespace PitWall.Web.Services;

/// <summary>Sección <c>PitWall</c> de la configuración que usa la UI (docs/configuration.md §1).</summary>
public sealed class PitWallOptions
{
    public const string SectionName = "PitWall";

    /// <summary>Sesión que se carga al arrancar si está ingerida; si no, se carga la primera disponible.</summary>
    [Range(1, int.MaxValue)]
    public int? DefaultSessionKey { get; set; }

    /// <summary>Piloto foco: su fila se resalta con el color de equipo.</summary>
    [Range(1, 99)]
    public int FocusDriver { get; set; } = 43;
}
