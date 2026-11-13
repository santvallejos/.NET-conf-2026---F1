using System.Text.Json;

namespace PitWall.Jev.Serialization;

/// <summary>Opciones de las preguntas en UPPER_SNAKE, como los enums del contrato (p. ej. <c>BoxNextLap</c> ↔ <c>BOX_NEXT_LAP</c>).</summary>
public static class JevContractNames
{
    public static string Of<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        JsonNamingPolicy.SnakeCaseUpper.ConvertName(value.ToString());

    public static IReadOnlyList<string> Of<TEnum>(IEnumerable<TEnum> values)
        where TEnum : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(values);

        return [.. values.Select(Of)];
    }

    public static bool TryParse<TEnum>(string? name, out TEnum value)
        where TEnum : struct, Enum
    {
        foreach (var candidate in Enum.GetValues<TEnum>())
        {
            if (string.Equals(Of(candidate), name, StringComparison.Ordinal))
            {
                value = candidate;
                return true;
            }
        }

        value = default;
        return false;
    }
}
