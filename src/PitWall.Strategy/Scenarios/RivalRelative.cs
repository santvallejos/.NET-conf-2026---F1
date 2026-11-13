using System.Text.Json.Serialization;

using PitWall.Domain.Serialization;

namespace PitWall.Strategy.Scenarios;

/// <summary>Posición del rival que choca respecto al piloto de referencia.</summary>
[JsonConverter(typeof(UpperSnakeEnumConverter<RivalRelative>))]
public enum RivalRelative
{
    Ahead,
    Behind,
}
