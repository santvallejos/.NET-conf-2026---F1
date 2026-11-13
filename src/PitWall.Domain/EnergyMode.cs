using System.Text.Json.Serialization;

using PitWall.Domain.Serialization;

namespace PitWall.Domain;

[JsonConverter(typeof(UpperSnakeEnumConverter<EnergyMode>))]
public enum EnergyMode
{
    Harvest,
    Balanced,
    Deploy,
    Overtake,
}
