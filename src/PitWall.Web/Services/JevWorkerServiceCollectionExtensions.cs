namespace PitWall.Web.Services;

public static class JevWorkerServiceCollectionExtensions
{
    /// <summary>
    /// Registra la cola de disparos, el historial en memoria y el worker que consulta a Jev (triage y estrategia).
    /// Requiere <c>AddPitWallStorage</c>, <c>AddPitWallJev</c>, <c>AddPitWallUi</c> y <c>AddPitWallStrategy</c>.
    /// </summary>
    public static IServiceCollection AddPitWallJevWorker(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<JevTriggers>();
        services.AddSingleton<JevDecisionStore>();
        services.AddHostedService<JevWorker>();
        return services;
    }
}
