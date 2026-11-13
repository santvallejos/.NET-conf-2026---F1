using System.Text.Json.Serialization;

using PitWall.Domain.Serialization;

namespace PitWall.Strategy.Rules;

[JsonConverter(typeof(UpperSnakeEnumConverter<DamageSeverity>))]
public enum DamageSeverity
{
    None,
    Minor,
    Major,
}
