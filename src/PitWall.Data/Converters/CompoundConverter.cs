using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using PitWall.Domain;

namespace PitWall.Data.Converters;

/// <summary>Guarda <see cref="Compound"/> como texto en mayúsculas (<c>SOFT</c>, <c>MEDIUM</c>, <c>HARD</c>, <c>INTERMEDIATE</c>, <c>WET</c>).</summary>
public sealed class CompoundConverter : ValueConverter<Compound, string>
{
    public CompoundConverter()
        : base(
            value => value.ToString().ToUpperInvariant(),
            text => Enum.Parse<Compound>(text, true))
    {
    }
}
