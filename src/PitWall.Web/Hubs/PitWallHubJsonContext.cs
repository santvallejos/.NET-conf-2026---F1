using System.Text.Json;
using System.Text.Json.Serialization;

using PitWall.Domain;
using PitWall.Jev;
using PitWall.Web.Services;

namespace PitWall.Web.Hubs;

/// <summary>Metadatos JSON de los mensajes de <see cref="PitWallHub"/> (camelCase, como el protocolo JSON de SignalR).</summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(RaceStateDelta))]
[JsonSerializable(typeof(PlaybackState))]
[JsonSerializable(typeof(IReadOnlyList<SessionInfo>))]
[JsonSerializable(typeof(IReadOnlyList<RaceControlMessage>))]
[JsonSerializable(typeof(StrategySnapshot))]
[JsonSerializable(typeof(JevDecision))]
[JsonSerializable(typeof(IReadOnlyList<JevDecision>))]
[JsonSerializable(typeof(JevSettings))]
[JsonSerializable(typeof(JevMode))]
public sealed partial class PitWallHubJsonContext : JsonSerializerContext
{
}
