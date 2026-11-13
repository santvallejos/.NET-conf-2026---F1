using System.Text.Json.Serialization;

using PitWall.Domain.Serialization;

namespace PitWall.Domain;

[JsonConverter(typeof(UpperSnakeEnumConverter<Compound>))]
public enum Compound
{
    Soft,
    Medium,
    Hard,
    Intermediate,
    Wet,
}
