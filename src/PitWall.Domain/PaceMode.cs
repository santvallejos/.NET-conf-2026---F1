using System.Text.Json.Serialization;

using PitWall.Domain.Serialization;

namespace PitWall.Domain;

[JsonConverter(typeof(UpperSnakeEnumConverter<PaceMode>))]
public enum PaceMode
{
    Push,
    Manage,
    SaveTyres,
}
