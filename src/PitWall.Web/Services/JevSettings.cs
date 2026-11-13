using PitWall.Jev;

namespace PitWall.Web.Services;

/// <summary>Configuración de Jev vigente (incluidos los cambios en caliente) para los controles de la UI.</summary>
public sealed record JevSettings(
    bool Enabled,
    JevClientKind Client,
    JevMode Mode,
    IReadOnlyList<int> Laps,
    IReadOnlyList<int> Drivers,
    bool TriageEnabled)
{
    public static JevSettings From(JevOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new JevSettings(options.Enabled, options.Client, options.Mode, [.. options.Laps], [.. options.Drivers], options.Triage.Enabled);
    }
}
