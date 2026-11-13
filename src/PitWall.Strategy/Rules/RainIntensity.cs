using System.Text.Json.Serialization;

using PitWall.Domain.Serialization;

namespace PitWall.Strategy.Rules;

[JsonConverter(typeof(UpperSnakeEnumConverter<RainIntensity>))]
public enum RainIntensity
{
    None,
    Light,
    Heavy,
}
