using Microsoft.Extensions.Options;

using PitWall.Strategy.Energy;

namespace PitWall.Web.Components.Panels.Energy;

public static class EnergyServiceCollectionExtensions
{
    /// <summary>
    /// Registra <see cref="EnergyOptions"/> (<c>Strategy:Energy</c>), el modelo de energía estimada y el servicio del
    /// panel Energía. Requiere <c>AddPitWallStorage</c>.
    /// </summary>
    public static IServiceCollection AddPitWallEnergy(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<EnergyOptions>()
            .Bind(configuration.GetSection(EnergyOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton(provider => new EnergyModel(provider.GetRequiredService<IOptions<EnergyOptions>>().Value));
        services.AddSingleton<EnergyEstimateService>();
        return services;
    }
}
