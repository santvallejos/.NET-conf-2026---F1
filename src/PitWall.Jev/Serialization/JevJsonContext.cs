using System.Text.Json.Serialization;

using PitWall.Jev.State;

namespace PitWall.Jev.Serialization;

/// <summary>Contrato JSON de Jev: claves en snake_case, nulls omitidos (docs/jev.md §2, §3.3).</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(JevRequest))]
[JsonSerializable(typeof(Dictionary<string, JevAnswer>))]
[JsonSerializable(typeof(List<JevMonteCarloEntry>))]
public sealed partial class JevJsonContext : JsonSerializerContext
{
}
