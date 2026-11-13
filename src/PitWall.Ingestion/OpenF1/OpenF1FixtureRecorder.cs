using System.Text;

namespace PitWall.Ingestion.OpenF1;

/// <summary>
/// Graba cada respuesta JSON de OpenF1 como fixture para los tests. El nombre del archivo sale del request
/// (<see cref="FileNameFor"/>), así un stub puede servir el fixture a partir de la misma URL.
/// Con <c>directory</c> en <c>null</c> no graba nada.
/// </summary>
public sealed class OpenF1FixtureRecorder(string? directory)
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public bool IsEnabled => directory is not null;

    public async Task RecordAsync(Uri requestUri, string json, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requestUri);

        if (directory is null)
        {
            return;
        }

        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, FileNameFor(requestUri));
        await File.WriteAllTextAsync(path, json, Utf8NoBom, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// <c>{endpoint}_{session_key}[_{driver_number}][_{inicio de ventana}].json</c>,
    /// p. ej. <c>car_data_11412_43_20261108T170000.json</c>.
    /// </summary>
    public static string FileNameFor(Uri requestUri)
    {
        ArgumentNullException.ThrowIfNull(requestUri);

        var endpoint = requestUri.Segments[^1].TrimEnd('/');
        var parts = new List<string> { endpoint };
        var query = ParseQuery(requestUri.Query);

        if (query.TryGetValue("session_key", out var sessionKey))
        {
            parts.Add(sessionKey);
        }

        if (query.TryGetValue("driver_number", out var driverNumber))
        {
            parts.Add(driverNumber);
        }

        if (query.TryGetValue("date>", out var windowStart))
        {
            parts.Add(windowStart.Replace("-", string.Empty, StringComparison.Ordinal).Replace(":", string.Empty, StringComparison.Ordinal));
        }

        return $"{string.Join('_', parts)}.json";
    }

    // El filtro de rango llega como "date%3E=..." / "date%3C=...": la clave desescapada es "date>" / "date<".
    private static Dictionary<string, string> ParseQuery(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            if (separator > 0)
            {
                values[Uri.UnescapeDataString(pair[..separator])] = Uri.UnescapeDataString(pair[(separator + 1)..]);
            }
        }

        return values;
    }
}
