using System.Globalization;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PitWall.Data.Converters;

/// <summary>
/// Guarda <see cref="DateTimeOffset"/> como texto ISO 8601 en UTC de ancho fijo.
/// SQLite no traduce comparaciones ni ORDER BY sobre <see cref="DateTimeOffset"/>; con este formato
/// el orden lexicográfico coincide con el cronológico y los filtros por rango de fecha se ejecutan en la base.
/// </summary>
public sealed class UtcDateTimeOffsetConverter : ValueConverter<DateTimeOffset, string>
{
    public const string Format = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";

    public UtcDateTimeOffsetConverter()
        : base(
            value => value.UtcDateTime.ToString(Format, CultureInfo.InvariantCulture),
            text => DateTimeOffset.ParseExact(text, Format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal))
    {
    }
}
