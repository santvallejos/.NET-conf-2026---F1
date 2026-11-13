using System.Text.Json.Serialization;

namespace PitWall.Jev;

/// <summary>Implementación seleccionada con <c>Jev:Client</c>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<JevClientKind>))]
public enum JevClientKind
{
    Fake,
    Real,
}
