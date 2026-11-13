using System.Text.Json.Serialization;

using PitWall.Domain.Serialization;

namespace PitWall.Domain;

[JsonConverter(typeof(UpperSnakeEnumConverter<PitCall>))]
public enum PitCall
{
    StayOut,
    Box,
    BoxNextLap,
}
