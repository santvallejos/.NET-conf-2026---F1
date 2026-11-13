namespace PitWall.Web.Components.Panels.Laps;

public static class LapsServiceCollectionExtensions
{
    /// <summary>Registra el servicio de timing por vuelta. Requiere <c>AddPitWallStorage</c>.</summary>
    public static IServiceCollection AddPitWallLaps(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<LapTimingService>();
        return services;
    }
}
