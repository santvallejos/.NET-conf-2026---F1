using Microsoft.Extensions.Options;

using PitWall.Strategy.Degradation;

namespace PitWall.Web.Components.Panels.Tyres;

public static class TyresServiceCollectionExtensions
{
    /// <summary>
    /// Registra <see cref="DegradationOptions"/>, el modelo de degradación y el servicio del panel Neumáticos.
    /// Requiere <c>AddPitWallStorage</c>.
    /// </summary>
    public static IServiceCollection AddPitWallTyres(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<DegradationOptions>()
            .Bind(configuration.GetSection(DegradationOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton(provider => new TyreDegradationModel(provider.GetRequiredService<IOptions<DegradationOptions>>().Value));
        services.AddSingleton<TyreDegradationService>();
        return services;
    }
}
