namespace PitWall.Web.Components.Panels.Map;

public static class MapServiceCollectionExtensions
{
    /// <summary>Registra el servicio del panel Mapa. Requiere <c>AddPitWallStorage</c>.</summary>
    public static IServiceCollection AddPitWallMap(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<TrackMapService>();
        return services;
    }
}
