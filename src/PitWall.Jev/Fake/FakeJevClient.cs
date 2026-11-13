using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

using PitWall.Domain;
using PitWall.Jev.Serialization;
using PitWall.Jev.State;

namespace PitWall.Jev.Fake;

/// <summary>
/// Cliente determinista sin red y de costo $0 (docs/jev.md §5). En cada pregunta <c>choice</c> elige la
/// opción implicada por la candidata de Monte Carlo con mejor <c>exp_pos</c> del <c>state</c>; si ninguna
/// candidata implica una opción de la pregunta, elige la primera.
/// </summary>
public sealed partial class FakeJevClient : IJevClient
{
    // Confianza fija de las preguntas que Monte Carlo no informa (p. ej. pace_mode, impact).
    private const double DefaultConfidence = 0.7;

    private const int ProbabilityDecimals = 4;

    private static readonly string StayOut = ToContractName(PitCall.StayOut);
    private static readonly string Box = ToContractName(PitCall.Box);
    private static readonly string BoxNextLap = ToContractName(PitCall.BoxNextLap);

    public Task<JevResponse> SystemOneAsync(JevRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        var candidates = ReadMonteCarlo(request.State);
        var answers = new Dictionary<string, JevAnswer>(StringComparer.Ordinal);

        foreach (var (key, question) in request.Questions)
        {
            var answer = question.Type switch
            {
                JevQuestionType.Choice => AnswerChoice(question.Criteria ?? [], candidates),
                JevQuestionType.Score => new JevAnswer(null, 1, null, DefaultConfidence),
                JevQuestionType.Noul => new JevAnswer(null, 0, null, DefaultConfidence),
                _ => null,
            };

            if (answer is not null)
            {
                answers[key] = answer;
            }
        }

        var response = new JevResponse(answers)
        {
            Tokens = JevTokenEstimator.Estimate(JevSerializer.SerializeRequest(request)),
        };

        return Task.FromResult(response);
    }

    private static JevAnswer? AnswerChoice(IReadOnlyList<string> criteria, IReadOnlyList<JevMonteCarloEntry> candidates)
    {
        if (criteria.Count == 0)
        {
            return null;
        }

        // Mejor exp_pos por opción entre las candidatas que la implican.
        var bestByOption = new Dictionary<string, double>(StringComparer.Ordinal);
        string? chosen = null;
        var bestOverall = double.MaxValue;

        foreach (var candidate in candidates)
        {
            var option = ImpliedOptions(candidate.Id).FirstOrDefault(criteria.Contains);
            if (option is null)
            {
                continue;
            }

            if (!bestByOption.TryGetValue(option, out var current) || candidate.ExpPos < current)
            {
                bestByOption[option] = candidate.ExpPos;
            }

            if (candidate.ExpPos < bestOverall)
            {
                bestOverall = candidate.ExpPos;
                chosen = option;
            }
        }

        return chosen is null
            ? AnswerWithoutMonteCarlo(criteria)
            : AnswerFromMonteCarlo(criteria, bestByOption, chosen, bestOverall, candidates.Max(c => c.ExpPos));
    }

    // Probabilidades derivadas: softmax de -(exp_pos - mejor) por opción; las opciones sin candidata
    // pesan como una posición peor que la peor candidata.
    private static JevAnswer AnswerFromMonteCarlo(
        IReadOnlyList<string> criteria,
        Dictionary<string, double> bestByOption,
        string chosen,
        double bestOverall,
        double worstOverall)
    {
        var weights = criteria.Distinct(StringComparer.Ordinal).ToDictionary(
            option => option,
            option => Math.Exp(-((bestByOption.TryGetValue(option, out var pos) ? pos : worstOverall + 1) - bestOverall)),
            StringComparer.Ordinal);
        var total = weights.Values.Sum();
        var probabilities = weights.ToDictionary(
            pair => pair.Key,
            pair => Math.Round(pair.Value / total, ProbabilityDecimals),
            StringComparer.Ordinal);

        return new JevAnswer(chosen, null, probabilities, probabilities[chosen]);
    }

    private static JevAnswer AnswerWithoutMonteCarlo(IReadOnlyList<string> criteria)
    {
        var chosen = criteria[0];
        var others = criteria.Count == 1 ? 0 : Math.Round((1 - DefaultConfidence) / (criteria.Count - 1), ProbabilityDecimals);
        var confidence = criteria.Count == 1 ? 1 : DefaultConfidence;
        var probabilities = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var option in criteria)
        {
            probabilities.TryAdd(option, option == chosen ? confidence : others);
        }

        return new JevAnswer(chosen, null, probabilities, confidence);
    }

    // Ids de docs/strategy-engine.md §4.1: STAY_OUT o BOX_L[+k]_<COMPUESTO>. Boxear en L+2 o más es
    // quedarse fuera esta vuelta.
    private static IReadOnlyList<string> ImpliedOptions(string strategyId)
    {
        var match = BoxStrategyId().Match(strategyId);
        if (!match.Success)
        {
            return [strategyId];
        }

        var lapOffset = match.Groups["offset"].Success
            ? int.Parse(match.Groups["offset"].Value, CultureInfo.InvariantCulture)
            : 0;
        var call = lapOffset switch
        {
            0 => Box,
            1 => BoxNextLap,
            _ => StayOut,
        };

        return [call, match.Groups["compound"].Value];
    }

    private static IReadOnlyList<JevMonteCarloEntry> ReadMonteCarlo(string state)
    {
        try
        {
            using var document = JsonDocument.Parse(state);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("mc", out var mc)
                && mc.ValueKind == JsonValueKind.Array
                    ? JevSerializer.DeserializeMonteCarlo(mc)
                    : [];
        }
        catch (JsonException)
        {
            // El state también puede ser texto libre (triage): sin Monte Carlo.
            return [];
        }
    }

    private static string ToContractName(PitCall call) => JsonNamingPolicy.SnakeCaseUpper.ConvertName(call.ToString());

    [GeneratedRegex(@"^BOX_L(?:\+(?<offset>\d+))?_(?<compound>[A-Z_]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex BoxStrategyId();
}
