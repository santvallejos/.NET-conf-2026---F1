using System.Net.Http.Headers;
using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PitWall.Jev.Real;

/// <summary>
/// Cliente de la API real de TypeSafe: <c>POST {Jev:BaseUrl}systemone</c> con <c>Authorization: Bearer {Jev:ApiKey}</c>
/// (docs/jev.md §5). <see cref="JevResponse.Tokens"/> son los tokens de entrada que informa la API, los únicos que se
/// facturan. Todo fallo (key ausente, red, <c>Jev:TimeoutMs</c>, HTTP no exitoso, respuesta fuera de contrato) sale como
/// <see cref="JevClientException"/>, que el decorador de costo convierte en alerta.
/// </summary>
public sealed partial class JevClient : IJevClient
{
    /// <summary>Cliente HTTP con nombre: hereda la resiliencia de <c>PitWall.ServiceDefaults</c>.</summary>
    public const string HttpClientName = "PitWall.Jev.Real";

    private const string SystemOnePath = "systemone";
    private const string JsonMediaType = "application/json";
    private const int MaxErrorDetailChars = 300;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<JevOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<JevClient> _logger;

    // Se registra como singleton (lo envuelve el decorador de costo): pide un HttpClient por llamada a la factory para
    // que los handlers roten, en vez de retener uno tipado para siempre.
    public JevClient(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<JevOptions> options,
        TimeProvider timeProvider,
        ILogger<JevClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClientFactory = httpClientFactory;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<JevResponse> SystemOneAsync(JevRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var options = _options.CurrentValue;
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new JevClientException("Falta Jev:ApiKey (parámetro secreto typesafe-api-key de Aspire).");
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, SystemOnePath)
        {
            Content = new ByteArrayContent(SystemOneWireMapper.SerializeRequest(request)),
        };
        message.Content.Headers.ContentType = new MediaTypeHeaderValue(JsonMediaType);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonMediaType));

        // Jev:TimeoutMs cubre la llamada completa, reintentos de la resiliencia incluidos.
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(options.TimeoutMs), _timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);

        try
        {
            using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(message, linked.Token);
            var body = await response.Content.ReadAsStringAsync(linked.Token);
            if (!response.IsSuccessStatusCode)
            {
                throw new JevClientException(
                    $"TypeSafe respondió {(int)response.StatusCode} {response.ReasonPhrase}: {Truncate(body)}");
            }

            var wire = SystemOneWireMapper.DeserializeResponse(body);
            var result = SystemOneWireMapper.ToResponse(request, wire);
            LogAnswered(wire!.Model ?? "?", result.Tokens, wire.Usage?.OutputTokens ?? 0);

            return result;
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new JevClientException($"TypeSafe no respondió en Jev:TimeoutMs={options.TimeoutMs} ms.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new JevClientException("No se pudo llamar a la API de TypeSafe.", ex);
        }
        catch (JsonException ex)
        {
            throw new JevClientException("La respuesta de TypeSafe no es JSON válido.", ex);
        }
    }

    private static string Truncate(string body) =>
        body.Length <= MaxErrorDetailChars ? body : $"{body[..MaxErrorDetailChars]}…";

    [LoggerMessage(Level = LogLevel.Debug, Message = "Jev respondió con el modelo {Model}: {InputTokens} tokens de entrada, {OutputTokens} de salida.")]
    private partial void LogAnswered(string model, int inputTokens, int outputTokens);
}
