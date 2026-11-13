using System.Text.Json.Serialization;

namespace PitWall.Domain;

// Serializado con el nombre del miembro ("Strategy"), como en el ejemplo JSON de docs/jev.md §4.
[JsonConverter(typeof(JsonStringEnumConverter<JevKind>))]
public enum JevKind
{
    Triage,
    Strategy,
}
