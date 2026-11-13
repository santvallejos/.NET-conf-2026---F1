using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using PitWall.Jev.Caching;
using PitWall.Jev.Serialization;

namespace PitWall.Jev.Guards;

/// <summary>
/// Decorador de costo (docs/jev.md §5, ADR-012), en este orden: <c>Jev:Enabled</c>, caché por hash del
/// request normalizado, corte en <c>Jev:MaxRequestsPerSession</c> y, si llama, medición de latencia,
/// tokens y costo. Los fallos del cliente se convierten en alerta: nunca tumban la app.
/// Solo las llamadas al cliente consumen presupuesto (las respuestas de caché no). El presupuesto es de la sesión
/// de replay activa: un request de otra sesión la reemplaza y arranca con el contador en cero.
/// </summary>
public sealed partial class GuardedJevClient : IJevClient
{
    private const decimal TokensPerMillion = 1_000_000m;

    private readonly IJevClient _inner;
    private readonly JevClientKind _clientKind;
    private readonly IJevCacheStore _cache;
    private readonly IOptionsMonitor<JevOptions> _options;
    private readonly JevMetrics _metrics;
    private readonly JevAlerts _alerts;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GuardedJevClient> _logger;
    private readonly Lock _budgetLock = new();
    private int? _activeSessionKey;
    private int _requests;

    public GuardedJevClient(
        IJevClient inner,
        JevClientKind clientKind,
        IJevCacheStore cache,
        IOptionsMonitor<JevOptions> options,
        JevMetrics metrics,
        JevAlerts alerts,
        TimeProvider timeProvider,
        ILogger<GuardedJevClient> logger)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(alerts);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _inner = inner;
        _clientKind = clientKind;
        _cache = cache;
        _options = options;
        _metrics = metrics;
        _alerts = alerts;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Llamadas al cliente consumidas por la sesión mientras es la activa (los aciertos de caché no cuentan);
    /// 0 para cualquier otra.
    /// </summary>
    public int RequestCount(int sessionKey)
    {
        lock (_budgetLock)
        {
            return _activeSessionKey == sessionKey ? _requests : 0;
        }
    }

    public async Task<JevResponse> SystemOneAsync(JevRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var options = _options.CurrentValue;
        if (!options.Enabled)
        {
            return JevResponse.NotCalled(JevResponseStatus.Disabled);
        }

        ActivateSession(request.SessionKey);

        var stateHash = JevSerializer.ComputeStateHash(request);
        if (options.Cache.Enabled)
        {
            var cached = await _cache.GetAsync(stateHash, ct);
            if (cached is not null)
            {
                _metrics.RecordCacheHit(request.Kind, _clientKind);
                return new JevResponse(JevSerializer.DeserializeAnswers(cached)) { FromCache = true };
            }
        }

        if (!TryConsumeBudget(options.MaxRequestsPerSession))
        {
            if (_alerts.Raise(new JevAlert(JevAlertKind.RequestLimitReached, request.SessionKey, _timeProvider.GetUtcNow())))
            {
                LogRequestLimitReached(request.SessionKey, options.MaxRequestsPerSession);
            }

            return JevResponse.NotCalled(JevResponseStatus.RequestLimitReached);
        }

        var startedAt = _timeProvider.GetTimestamp();
        JevResponse response;
        try
        {
            response = await _inner.SystemOneAsync(request, ct);
        }
        catch (JevClientException ex)
        {
            LogClientFailed(ex, request.SessionKey, _clientKind);
            _alerts.Raise(new JevAlert(JevAlertKind.ClientFailed, request.SessionKey, _timeProvider.GetUtcNow()));
            return JevResponse.NotCalled(JevResponseStatus.Failed);
        }

        var latencyMs = (int)Math.Round(_timeProvider.GetElapsedTime(startedAt).TotalMilliseconds);
        var costUsd = _clientKind == JevClientKind.Real
            ? response.Tokens * options.PricePerMillionInputTokensUsd / TokensPerMillion
            : 0m;

        _metrics.RecordCall(request.Kind, _clientKind, latencyMs, response.Tokens, costUsd);

        if (options.Cache.Enabled)
        {
            await _cache.SetAsync(stateHash, request.Kind, JevSerializer.SerializeAnswers(response.Answers), ct);
        }

        return response with { FromCache = false, LatencyMs = latencyMs, CostUsd = costUsd };
    }

    // Cambio de sesión de replay: contador en cero y fuera la alerta de límite de la sesión que entra, por si ya
    // estuvo activa antes y llegó al tope.
    private void ActivateSession(int sessionKey)
    {
        lock (_budgetLock)
        {
            if (_activeSessionKey == sessionKey)
            {
                return;
            }

            _activeSessionKey = sessionKey;
            _requests = 0;
        }

        _alerts.Resolve(JevAlertKind.RequestLimitReached, sessionKey);
    }

    private bool TryConsumeBudget(int maxRequests)
    {
        lock (_budgetLock)
        {
            if (_requests >= maxRequests)
            {
                return false;
            }

            _requests++;
            return true;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Jev alcanzó MaxRequestsPerSession={MaxRequests} en la sesión {SessionKey}; no se llama más.")]
    private partial void LogRequestLimitReached(int sessionKey, int maxRequests);

    [LoggerMessage(Level = LogLevel.Error, Message = "Falló el cliente Jev {Client} en la sesión {SessionKey}.")]
    private partial void LogClientFailed(Exception exception, int sessionKey, JevClientKind client);
}
