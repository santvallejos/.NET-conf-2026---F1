using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace PitWall.Ingestion.OpenF1;

/// <summary>
/// Cliente HTTP de OpenF1. Un 404 de OpenF1 significa "sin resultados" y se devuelve como lista vacía;
/// cualquier otro fallo (tras los reintentos de <see cref="OpenF1RateLimitHandler"/>) lanza <see cref="OpenF1IngestionException"/>.
/// </summary>
public sealed class OpenF1Client(HttpClient httpClient, OpenF1FixtureRecorder recorder)
{
    private const string DateFilterFormat = "yyyy-MM-dd'T'HH:mm:ss";

    public async Task<IReadOnlyList<T>> GetAsync<T>(
        string endpoint,
        int sessionKey,
        JsonTypeInfo<List<T>> typeInfo,
        CancellationToken cancellationToken,
        int? driverNumber = null,
        TelemetryWindow? window = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentNullException.ThrowIfNull(typeInfo);

        var requestUri = BuildRequestUri(endpoint, sessionKey, driverNumber, window);
        using var response = await SendAsync(requestUri, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        EnsureSuccess(response, requestUri);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (recorder.IsEnabled)
        {
            var absoluteUri = httpClient.BaseAddress is { } baseAddress ? new Uri(baseAddress, requestUri) : requestUri;
            await recorder.RecordAsync(absoluteUri, json, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            return JsonSerializer.Deserialize(json, typeInfo) ?? [];
        }
        catch (JsonException exception)
        {
            throw new OpenF1IngestionException($"Respuesta inválida de OpenF1 en {requestUri}.", exception);
        }
    }

    /// <summary>Descarga un archivo (mp3 de <c>team_radio</c>); <c>null</c> si no existe (404).</summary>
    public async Task<byte[]?> DownloadAsync(Uri fileUri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fileUri);

        using var response = await SendAsync(fileUri, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        EnsureSuccess(response, fileUri);
        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// <c>{endpoint}?session_key=..[&amp;driver_number=..][&amp;date&gt;=..&amp;date&lt;..]</c>; el rango va en UTC sin offset,
    /// como lo espera OpenF1. El operador va pegado a la clave (<c>date%3E=</c> es <c>&gt;=</c>, <c>date%3C</c> es <c>&lt;</c>):
    /// el fin exclusivo evita que ventanas consecutivas repitan la muestra del borde.
    /// </summary>
    public static Uri BuildRequestUri(string endpoint, int sessionKey, int? driverNumber = null, TelemetryWindow? window = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        var uri = string.Create(CultureInfo.InvariantCulture, $"{endpoint}?session_key={sessionKey}");
        if (driverNumber is { } number)
        {
            uri += string.Create(CultureInfo.InvariantCulture, $"&driver_number={number}");
        }

        if (window is { } range)
        {
            uri += $"&date%3E={FormatDate(range.Start)}&date%3C{FormatDate(range.End)}";
        }

        return new Uri(uri, UriKind.Relative);
    }

    private static string FormatDate(DateTimeOffset value) =>
        value.UtcDateTime.ToString(DateFilterFormat, CultureInfo.InvariantCulture);

    private static void EnsureSuccess(HttpResponseMessage response, Uri requestUri)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new OpenF1IngestionException($"OpenF1 respondió {(int)response.StatusCode} a {requestUri} tras los reintentos.");
        }
    }

    private async Task<HttpResponseMessage> SendAsync(Uri requestUri, CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.GetAsync(requestUri, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new OpenF1IngestionException($"No se pudo conectar con {requestUri}.", exception);
        }
    }
}
