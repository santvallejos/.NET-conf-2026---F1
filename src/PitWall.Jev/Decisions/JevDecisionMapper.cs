using PitWall.Domain;
using PitWall.Jev.Requests;
using PitWall.Jev.Serialization;

namespace PitWall.Jev.Decisions;

/// <summary><see cref="JevResponse"/> → <see cref="JevDecision"/> (docs/jev.md §4).</summary>
public static class JevDecisionMapper
{
    private const int MinUrgency = 1;
    private const int MaxUrgency = 5;

    /// <summary>
    /// Decisión con confidence = mínimo de las confidences de las preguntas respondidas; null si Jev no respondió.
    /// Una opción que no se ofreció en el request (p. ej. una prohibida por las reglas) se descarta.
    /// </summary>
    public static JevDecision? Map(JevDecisionInput input, JevRequest request, JevResponse response)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);

        var answers = response.Answers
            .Where(answer => request.Questions.ContainsKey(answer.Key))
            .ToDictionary(StringComparer.Ordinal);
        if (response.Status != JevResponseStatus.Answered || answers.Count == 0)
        {
            return null;
        }

        var probabilities = answers
            .Where(answer => answer.Value.Probabilities is not null)
            .ToDictionary(
                answer => answer.Key,
                answer => (IReadOnlyDictionary<string, double>)answer.Value.Probabilities!,
                StringComparer.Ordinal);

        return new JevDecision(
            input.Id,
            input.SessionKey,
            input.ScenarioId,
            input.DriverNumber,
            input.Lap,
            request.Kind,
            input.Trigger,
            Choice<PitCall>(JevQuestionKeys.PitCall),
            Choice<Compound>(JevQuestionKeys.Compound),
            Choice<PaceMode>(JevQuestionKeys.PaceMode),
            Choice<EnergyMode>(JevQuestionKeys.EnergyMode),
            Choice<Impact>(JevQuestionKeys.Impact),
            Urgency(),
            answers.Values.Min(answer => answer.Confidence),
            probabilities,
            input.Reasons,
            Hypothetical: input.ScenarioId is not null,
            response.FromCache,
            response.LatencyMs,
            response.Tokens,
            response.CostUsd);

        TEnum? Choice<TEnum>(string key)
            where TEnum : struct, Enum =>
            answers.TryGetValue(key, out var answer)
                && request.Questions[key].Criteria?.Contains(answer.Choice, StringComparer.Ordinal) == true
                && JevContractNames.TryParse<TEnum>(answer.Choice, out var value)
                    ? value
                    : null;

        int? Urgency() =>
            answers.TryGetValue(JevQuestionKeys.Urgency, out var answer) && answer.Score is { } score && double.IsFinite(score)
                ? Math.Clamp((int)Math.Round(score, MidpointRounding.AwayFromZero), MinUrgency, MaxUrgency)
                : null;
    }
}
