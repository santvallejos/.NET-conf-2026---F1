using PitWall.Replay;

namespace PitWall.Web.Services;

/// <summary>
/// Worker que se suscribe al replay, carga la sesión inicial y pasa cada <c>RaceState</c> publicado a
/// <see cref="PitWallSession"/>, que actualiza los paneles y empuja el delta por el hub.
/// </summary>
public sealed class PitWallBroadcaster(ReplayEngine engine, PitWallSession session, ILogger<PitWallBroadcaster> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Suscripción antes de cargar: el estado inicial de la sesión llega por el canal como cualquier otro.
        using var subscription = engine.Subscribe();
        try
        {
            await session.InitializeAsync(stoppingToken);
            await foreach (var state in subscription.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await session.PublishAsync(state);
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Publishing race state at {Clock} failed", state.Clock);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
