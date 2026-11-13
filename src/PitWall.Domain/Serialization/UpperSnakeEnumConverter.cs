using System.Text.Json;
using System.Text.Json.Serialization;

namespace PitWall.Domain.Serialization;

/// <summary>
/// Serializa los valores del enum en UPPER_SNAKE (p. ej. <c>Intermediate</c> ↔ <c>"INTERMEDIATE"</c>),
/// formato del contrato público / OpenF1.
/// </summary>
public sealed class UpperSnakeEnumConverter<TEnum> : JsonStringEnumConverter<TEnum>
    where TEnum : struct, Enum
{
    public UpperSnakeEnumConverter()
        : base(JsonNamingPolicy.SnakeCaseUpper, allowIntegerValues: false)
    {
    }
}
