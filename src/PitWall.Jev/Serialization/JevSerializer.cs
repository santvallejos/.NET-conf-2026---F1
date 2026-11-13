using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

using PitWall.Jev.State;

namespace PitWall.Jev.Serialization;

/// <summary>
/// Serialización canónica de Jev. El request se normaliza (preguntas en orden ordinal) para que el mismo
/// estado produzca siempre el mismo JSON y, por lo tanto, el mismo hash de caché (ADR-012).
/// </summary>
public static class JevSerializer
{
    // El estado viaja como string JSON: con el encoder por defecto cada comilla sería \u0022 y
    // multiplicaría los tokens facturados.
    private static readonly JevJsonContext Context = new(new JsonSerializerOptions(JevJsonContext.Default.Options)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    });

    public static JsonSerializerOptions Options => Context.Options;

    public static string SerializeRequest(JevRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalized = request with
        {
            Questions = new SortedDictionary<string, JevQuestion>(
                request.Questions.ToDictionary(),
                StringComparer.Ordinal),
        };

        return JsonSerializer.Serialize(normalized, Context.JevRequest);
    }

    /// <summary>SHA-256 en hexadecimal minúscula del request normalizado.</summary>
    public static string ComputeStateHash(JevRequest request)
    {
        var bytes = Encoding.UTF8.GetBytes(SerializeRequest(request));
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    public static string SerializeAnswers(IReadOnlyDictionary<string, JevAnswer> answers)
    {
        ArgumentNullException.ThrowIfNull(answers);

        return JsonSerializer.Serialize(answers.ToDictionary(), Context.DictionaryStringJevAnswer);
    }

    public static IReadOnlyDictionary<string, JevAnswer> DeserializeAnswers(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        return JsonSerializer.Deserialize(json, Context.DictionaryStringJevAnswer)
            ?? throw new JsonException("Respuesta de Jev vacía.");
    }

    public static string SerializeMonteCarlo(IEnumerable<JevMonteCarloEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return JsonSerializer.Serialize(entries.ToList(), Context.ListJevMonteCarloEntry);
    }

    /// <summary><c>state</c> de estrategia como JSON compacto.</summary>
    public static string SerializeState(JevStrategyState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return JsonSerializer.Serialize(state, Context.JevStrategyState);
    }

    /// <summary><c>state</c> de triage como JSON compacto.</summary>
    public static string SerializeState(JevTriageState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return JsonSerializer.Serialize(state, Context.JevTriageState);
    }

    public static IReadOnlyList<JevMonteCarloEntry> DeserializeMonteCarlo(JsonElement element) =>
        element.Deserialize(Context.ListJevMonteCarloEntry) ?? [];
}
