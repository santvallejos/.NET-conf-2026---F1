using System.Text.Json.Nodes;

namespace PitWall.Jev.Real;

/// <summary>Pregunta de la API de TypeSafe (schemas <c>ChoiceQuestion</c>, <c>ScoreQuestion</c>, <c>NoulQuestion</c>).</summary>
/// <param name="Criteria">
/// Objeto opción → descripción en <c>choice</c>, lista ordenada de niveles en <c>score</c>; se omite en <c>noul</c>.
/// </param>
internal sealed record SystemOneWireQuestion(
    JevQuestionType Type,
    string Instructions,
    JsonNode? Criteria);
