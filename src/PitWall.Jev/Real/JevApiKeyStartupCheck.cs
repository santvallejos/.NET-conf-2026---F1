using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using PitWall.Jev.Guards;

namespace PitWall.Jev.Real;

/// <summary>
/// Al arrancar con <c>Jev:Client = Real</c> sin <c>Jev:ApiKey</c>: deja un warning en el log y levanta la alerta de
/// cliente caído en la sesión indicada, para que la UI la muestre. No bloquea el arranque.
/// </summary>
public sealed partial class JevApiKeyStartupCheck : IHostedService
{
    private readonly IOptionsMonitor<JevOptions> _options;
    private readonly JevAlerts _alerts;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<JevApiKeyStartupCheck> _logger;
    private readonly int? _alertSessionKey;

    /// <param name="alertSessionKey">Sesión en la que se muestra la alerta; null solo deja el log.</param>
    public JevApiKeyStartupCheck(
        IOptionsMonitor<JevOptions> options,
        JevAlerts alerts,
        TimeProvider timeProvider,
        ILogger<JevApiKeyStartupCheck> logger,
        int? alertSessionKey)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(alerts);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _alerts = alerts;
        _timeProvider = timeProvider;
        _logger = logger;
        _alertSessionKey = alertSessionKey;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (JevApiKeyPostConfigure.IsMissingApiKey(_options.CurrentValue))
        {
            LogMissingApiKey();
            if (_alertSessionKey is { } sessionKey)
            {
                _alerts.Raise(new JevAlert(JevAlertKind.ClientFailed, sessionKey, _timeProvider.GetUtcNow()));
            }
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Jev:Client=Real sin Jev:ApiKey: Jev queda deshabilitado. Configurar el parámetro secreto typesafe-api-key o usar Jev:Client=Fake.")]
    private partial void LogMissingApiKey();
}
