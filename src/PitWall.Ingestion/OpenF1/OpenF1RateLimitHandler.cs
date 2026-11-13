using System.Net;

using Microsoft.Extensions.Logging;

namespace PitWall.Ingestion.OpenF1;

/// <summary>
/// Pausa <see cref="OpenF1Options.RequestDelay"/> entre requests y reintenta ante 429/5xx con backoff exponencial
/// (o el <c>Retry-After</c> del servidor). Agotados los reintentos devuelve la última respuesta.
/// La ingesta es secuencial: el handler no está pensado para requests concurrentes.
/// </summary>
public sealed partial class OpenF1RateLimitHandler(
    OpenF1Options options,
    TimeProvider timeProvider,
    ILogger<OpenF1RateLimitHandler> logger) : DelegatingHandler
{
    private bool _hasSentRequest;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_hasSentRequest)
        {
            await DelayAsync(options.RequestDelay, cancellationToken).ConfigureAwait(false);
        }

        _hasSentRequest = true;

        for (var attempt = 0; ; attempt++)
        {
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!IsTransient(response.StatusCode) || attempt >= options.MaxRetries)
            {
                return response;
            }

            var backoff = RetryDelay(response, attempt);
            LogRetry(logger, (int)response.StatusCode, request.RequestUri, backoff.TotalMilliseconds, attempt + 1, options.MaxRetries);
            response.Dispose();
            await DelayAsync(backoff, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.TooManyRequests || (int)statusCode >= 500;

    private TimeSpan RetryDelay(HttpResponseMessage response, int attempt)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta)
        {
            return delta;
        }

        if (retryAfter?.Date is { } date)
        {
            var wait = date - timeProvider.GetUtcNow();
            return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
        }

        return options.InitialBackoff * Math.Pow(2, attempt);
    }

    private Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        delay > TimeSpan.Zero ? Task.Delay(delay, timeProvider, cancellationToken) : Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Warning, Message = "OpenF1 respondió {StatusCode} a {RequestUri}; reintento en {DelayMs} ms ({Attempt}/{MaxRetries}).")]
    private static partial void LogRetry(ILogger logger, int statusCode, Uri? requestUri, double delayMs, int attempt, int maxRetries);
}
