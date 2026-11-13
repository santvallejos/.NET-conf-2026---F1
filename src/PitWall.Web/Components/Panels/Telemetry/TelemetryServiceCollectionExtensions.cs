namespace PitWall.Web.Components.Panels.Telemetry;

public static class TelemetryServiceCollectionExtensions
{
    /// <summary>Registra el servicio del panel Telemetría. Requiere <c>AddPitWallStorage</c>.</summary>
    public static IServiceCollection AddPitWallTelemetry(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<TelemetryService>();
        return services;
    }
}
