using System.Text.Json.Serialization;

namespace PitWall.Jev.Real;

/// <summary>Contrato JSON de la API de TypeSafe: claves en snake_case y propiedades null omitidas.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SystemOneWireRequest))]
[JsonSerializable(typeof(SystemOneWireResponse))]
internal sealed partial class JevRealJsonContext : JsonSerializerContext
{
}
