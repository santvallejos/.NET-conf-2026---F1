using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PitWall.Jev.Real;

/// <summary>
/// Traduce <see cref="JevRequest"/> / <see cref="JevResponse"/> al formato de la referencia oficial de
/// <c>POST /v1/systemone</c> (https://docs.typesafe.ai/api/system-one) y valida la respuesta contra ese contrato.
/// </summary>
internal static class SystemOneWireMapper
{
    // La API exige niveles explícitos en score (1 a 10, contados desde 0). Sin criterios propios la pregunta usa la
    // escala 1–5 de docs/jev.md §3.1, con etiquetas numéricas para devolver el score en esa misma escala.
    private static readonly IReadOnlyList<string> DefaultScoreLevels = ["1", "2", "3", "4", "5"];

    // El estado viaja como string JSON: sin el encoder relajado cada comilla sería ".
    private static readonly JevRealJsonContext Context = new(new JsonSerializerOptions(JevRealJsonContext.Default.Options)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    });

    public static byte[] SerializeRequest(JevRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var questions = request.Questions.ToDictionary(
            pair => pair.Key,
            pair => ToWire(pair.Key, pair.Value),
            StringComparer.Ordinal);

        return JsonSerializer.SerializeToUtf8Bytes(
            new SystemOneWireRequest(request.Model, request.State, questions),
            Context.SystemOneWireRequest);
    }

    public static SystemOneWireResponse? DeserializeResponse(string json) =>
        JsonSerializer.Deserialize(json, Context.SystemOneWireResponse);

    /// <summary>Respuestas por pregunta enviada; <see cref="JevResponse.Tokens"/> son los tokens de entrada facturados.</summary>
    public static JevResponse ToResponse(JevRequest request, SystemOneWireResponse? wire)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (wire?.Answers is null || wire.Usage?.InputTokens is not { } inputTokens || inputTokens < 0)
        {
            throw Invalid("faltan answers o usage.input_tokens");
        }

        var answers = new Dictionary<string, JevAnswer>(StringComparer.Ordinal);
        foreach (var (key, question) in request.Questions)
        {
            if (!wire.Answers.TryGetValue(key, out var answer))
            {
                throw Invalid($"falta la respuesta de '{key}'");
            }

            if (answer.Type != question.Type)
            {
                throw Invalid($"'{key}' llegó como {answer.Type?.ToString() ?? "null"}, se preguntó {question.Type}");
            }

            answers[key] = question.Type switch
            {
                JevQuestionType.Choice => ToChoice(key, question, answer),
                JevQuestionType.Score => ToScore(key, question, answer),
                JevQuestionType.Noul => ToNoul(key, answer),
                _ => throw Unsupported(key, question.Type),
            };
        }

        return new JevResponse(answers) { Tokens = inputTokens };
    }

    private static SystemOneWireQuestion ToWire(string key, JevQuestion question) => question.Type switch
    {
        JevQuestionType.Choice => new(question.Type, question.Instructions, ChoiceCriteria(key, question)),
        JevQuestionType.Score => new(question.Type, question.Instructions, new JsonArray([.. ScoreLevels(question).Select(level => (JsonNode?)level)])),
        JevQuestionType.Noul => new(question.Type, question.Instructions, null),
        _ => throw Unsupported(key, question.Type),
    };

    // Opciones sin descripción (valor null): la API las interpreta por su nombre.
    private static JsonObject ChoiceCriteria(string key, JevQuestion question)
    {
        if (question.Criteria is not { Count: > 0 } criteria)
        {
            throw new JevClientException($"La pregunta choice '{key}' no tiene opciones.");
        }

        var options = new JsonObject();
        foreach (var option in criteria.Distinct(StringComparer.Ordinal))
        {
            options[option] = null;
        }

        return options;
    }

    private static IReadOnlyList<string> ScoreLevels(JevQuestion question) =>
        question.Criteria is { Count: > 0 } criteria ? criteria : DefaultScoreLevels;

    private static JevAnswer ToChoice(string key, JevQuestion question, SystemOneWireAnswer answer)
    {
        if (answer.Choice is not { } choice || question.Criteria?.Contains(choice, StringComparer.Ordinal) != true)
        {
            throw Invalid($"'{key}' eligió una opción que no se envió");
        }

        if (answer.Confidence is not { } confidence || answer.Probabilities is not { } probabilities)
        {
            throw Invalid($"'{key}' sin confidence o probabilities");
        }

        return new JevAnswer(choice, null, probabilities.ToDictionary(StringComparer.Ordinal), confidence);
    }

    // La API cuenta los niveles desde 0. Si los niveles enviados son enteros consecutivos (p. ej. 1–5), el score
    // y las probabilidades se pasan a esa escala; si no, quedan en la de la API.
    private static JevAnswer ToScore(string key, JevQuestion question, SystemOneWireAnswer answer)
    {
        if (answer.Score is not { } score || answer.Confidence is not { } confidence
            || answer.Probabilities is not { } probabilities || answer.Legend is null)
        {
            throw Invalid($"'{key}' sin score, confidence, legend o probabilities");
        }

        var levels = ScoreLevels(question);
        var offset = ConsecutiveIntegerStart(levels);
        var relabeled = probabilities.ToDictionary(
            pair => LevelLabel(pair.Key, levels),
            pair => pair.Value,
            StringComparer.Ordinal);

        return new JevAnswer(null, offset is { } start ? score + start : score, relabeled, confidence);
    }

    // La API no informa confidence en noul: se usa |2p − 1|, la fórmula de la doc oficial de TypeSafe para
    // comparar un noul con un choice en la misma escala.
    private static JevAnswer ToNoul(string key, SystemOneWireAnswer answer)
    {
        if (answer.Noul is not { } yes || yes is < 0 or > 1)
        {
            throw Invalid($"'{key}' sin noul en [0, 1]");
        }

        return new JevAnswer(null, yes, null, Math.Abs((2 * yes) - 1));
    }

    private static int? ConsecutiveIntegerStart(IReadOnlyList<string> levels)
    {
        int? start = null;
        for (var index = 0; index < levels.Count; index++)
        {
            if (!int.TryParse(levels[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                return null;
            }

            start ??= value;
            if (value != start + index)
            {
                return null;
            }
        }

        return start;
    }

    private static string LevelLabel(string level, IReadOnlyList<string> levels) =>
        int.TryParse(level, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) && index >= 0 && index < levels.Count
            ? levels[index]
            : level;

    private static JevClientException Invalid(string detail) =>
        new($"Respuesta de TypeSafe fuera de contrato: {detail}.");

    private static JevClientException Unsupported(string key, JevQuestionType type) =>
        new($"La pregunta '{key}' es de tipo {type}, que el cliente real no envía.");
}
