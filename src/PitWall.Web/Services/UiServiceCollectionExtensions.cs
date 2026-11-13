using System.Text.Json.Serialization.Metadata;

using PitWall.Web.Hubs;

namespace PitWall.Web.Services;

public static class UiServiceCollectionExtensions
{
    /// <summary>
    /// Registra <see cref="PitWallOptions"/>, SignalR con el contrato JSON del hub, la sesión compartida por los
    /// paneles y el worker que la alimenta desde el replay. Requiere <c>AddPitWallReplay</c> y <c>AddPitWallStorage</c>.
    /// </summary>
    public static IServiceCollection AddPitWallUi(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<PitWallOptions>()
            .Bind(configuration.GetSection(PitWallOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // El resolver por reflexión queda detrás del contexto generado para los tipos de otros hubs (Blazor).
        services.AddSignalR().AddJsonProtocol(options =>
        {
            options.PayloadSerializerOptions.TypeInfoResolverChain.Insert(0, PitWallHubJsonContext.Default);
            options.PayloadSerializerOptions.TypeInfoResolverChain.Add(new DefaultJsonTypeInfoResolver());
        });
        services.AddSingleton<PitWallSession>();
        services.AddHostedService<PitWallBroadcaster>();
        return services;
    }
}
