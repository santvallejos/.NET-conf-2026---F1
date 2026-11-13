using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PitWall.Ingestion.OpenF1;

/// <summary>
/// Lee campos numéricos que OpenF1 a veces publica con otra forma: texto (<c>"+1 LAP"</c>) o lista
/// (duraciones por fase en clasificación). Lo que no es un número queda en <c>null</c>.
/// </summary>
public sealed class LenientDoubleConverter : JsonConverter<double?>
{
    public override double? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                return reader.GetDouble();
            case JsonTokenType.String:
                return double.TryParse(reader.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                    ? value
                    : null;
            case JsonTokenType.StartArray:
            case JsonTokenType.StartObject:
                reader.Skip();
                return null;
            default:
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, double? value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteNumberValue(value.Value);
        }
    }
}
