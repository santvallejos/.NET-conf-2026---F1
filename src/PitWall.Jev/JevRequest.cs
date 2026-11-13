using System.Text.Json.Serialization;

using PitWall.Domain;

namespace PitWall.Jev;

/// <summary>Request de <c>POST /v1/systemone</c> (docs/jev.md §2).</summary>
/// <param name="State">Estado serializado como JSON compacto.</param>
/// <param name="Questions">Preguntas por clave (p. ej. <c>pit_call</c>), evaluadas en paralelo sobre el mismo estado.</param>
public sealed record JevRequest(
    string Model,
    string State,
    IReadOnlyDictionary<string, JevQuestion> Questions)
{
    /// <summary>Tipo de consulta; no viaja a la API, etiqueta métricas y caché.</summary>
    [JsonIgnore]
    public JevKind Kind { get; init; } = JevKind.Strategy;

    /// <summary>Sesión que consume el presupuesto de <c>Jev:MaxRequestsPerSession</c>; no viaja a la API.</summary>
    [JsonIgnore]
    public int SessionKey { get; init; }
}
