using System.Text.Json;

namespace PitWall.Jev.Real;

/// <summary>Respuesta a una pregunta (schemas <c>ChoiceAnswer</c>, <c>ScoreAnswer</c>, <c>NoulAnswer</c>).</summary>
/// <param name="Score">Valor esperado sobre los niveles del <c>score</c>, contados desde 0.</param>
/// <param name="Noul">Probabilidad de "sí" de una pregunta <c>noul</c>; la API no informa confidence para este tipo.</param>
/// <param name="Legend">Nivel (<c>"0"</c>, <c>"1"</c>, …) → criterio enviado, solo en <c>score</c>.</param>
internal sealed record SystemOneWireAnswer(
    JevQuestionType? Type,
    string? Choice,
    double? Score,
    double? Noul,
    double? Confidence,
    IReadOnlyDictionary<string, double>? Probabilities,
    IReadOnlyDictionary<string, JsonElement>? Legend);
