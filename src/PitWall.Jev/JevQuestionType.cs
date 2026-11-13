using System.Text.Json.Serialization;

namespace PitWall.Jev;

[JsonConverter(typeof(JsonStringEnumConverter<JevQuestionType>))]
public enum JevQuestionType
{
    [JsonStringEnumMemberName("choice")]
    Choice,

    [JsonStringEnumMemberName("score")]
    Score,

    [JsonStringEnumMemberName("noul")]
    Noul,

    [JsonStringEnumMemberName("box")]
    Box,
}
