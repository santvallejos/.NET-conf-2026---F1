namespace PitWall.Jev;

/// <param name="Criteria">Opciones cerradas de una pregunta <c>choice</c>; null en el resto.</param>
public sealed record JevQuestion(
    JevQuestionType Type,
    string Instructions,
    IReadOnlyList<string>? Criteria = null);
