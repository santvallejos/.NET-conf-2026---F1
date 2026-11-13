using System.Text.Json.Serialization;

using PitWall.Domain.Serialization;

namespace PitWall.Domain;

[JsonConverter(typeof(UpperSnakeEnumConverter<Impact>))]
public enum Impact
{
    None,
    Monitor,
    ActNow,
}
