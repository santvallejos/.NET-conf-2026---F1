using System.Text.Json;
using System.Text.Json.Serialization;

using PitWall.Strategy.Scenarios;

namespace PitWall.Web.Storage;

/// <summary>JSON de <c>InjectedEvent.Params</c>: camelCase, sin nulls y enums en UPPER_SNAKE.</summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(InjectedEventParameters))]
internal sealed partial class ScenarioJsonContext : JsonSerializerContext
{
}
