namespace PitWall.Jev;

/// <summary>
/// Respuestas por clave de pregunta más la medición de la llamada. Cuando Jev no se llama
/// (<see cref="Status"/> distinto de <see cref="JevResponseStatus.Answered"/>), <see cref="Answers"/> está vacío.
/// </summary>
public sealed record JevResponse(IReadOnlyDictionary<string, JevAnswer> Answers)
{
    private static readonly IReadOnlyDictionary<string, JevAnswer> NoAnswers = new Dictionary<string, JevAnswer>();

    public JevResponseStatus Status { get; init; } = JevResponseStatus.Answered;

    public bool FromCache { get; init; }

    public int LatencyMs { get; init; }

    public int Tokens { get; init; }

    public decimal CostUsd { get; init; }

    public static JevResponse NotCalled(JevResponseStatus status) => new(NoAnswers) { Status = status };
}
