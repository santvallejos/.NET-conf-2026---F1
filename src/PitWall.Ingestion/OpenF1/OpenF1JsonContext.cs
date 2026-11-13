using System.Text.Json.Serialization;

namespace PitWall.Ingestion.OpenF1;

/// <summary>Respuestas de OpenF1: listas JSON con claves en snake_case.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(List<OpenF1Session>))]
[JsonSerializable(typeof(List<OpenF1Driver>))]
[JsonSerializable(typeof(List<OpenF1Lap>))]
[JsonSerializable(typeof(List<OpenF1Stint>))]
[JsonSerializable(typeof(List<OpenF1Pit>))]
[JsonSerializable(typeof(List<OpenF1RaceControl>))]
[JsonSerializable(typeof(List<OpenF1Weather>))]
[JsonSerializable(typeof(List<OpenF1Interval>))]
[JsonSerializable(typeof(List<OpenF1Position>))]
[JsonSerializable(typeof(List<OpenF1Overtake>))]
[JsonSerializable(typeof(List<OpenF1SessionResult>))]
[JsonSerializable(typeof(List<OpenF1TeamRadio>))]
[JsonSerializable(typeof(List<OpenF1StartingGrid>))]
[JsonSerializable(typeof(List<OpenF1CarData>))]
[JsonSerializable(typeof(List<OpenF1Location>))]
public sealed partial class OpenF1JsonContext : JsonSerializerContext
{
}
