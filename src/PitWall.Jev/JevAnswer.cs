namespace PitWall.Jev;

/// <summary>Respuesta de Jev a una pregunta (docs/jev.md §2).</summary>
/// <param name="Choice">Opción elegida en preguntas <c>choice</c>.</param>
/// <param name="Score">Valor en preguntas <c>score</c> / <c>noul</c>.</param>
public sealed record JevAnswer(
    string? Choice,
    double? Score,
    IReadOnlyDictionary<string, double>? Probabilities,
    double Confidence);
