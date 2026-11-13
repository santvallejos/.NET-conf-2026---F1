using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PitWall.Replay;

public static class ReplayServiceCollectionExtensions
{
    /// <summary>
    /// Registra <see cref="ReplayOptions"/>, el reloj y el worker del replay. Requiere un
    /// <c>IDbContextFactory&lt;PitWallDbContext&gt;</c> registrado por el host.
    /// </summary>
    public static IServiceCollection AddPitWallReplay(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<ReplayOptions>()
            .Bind(configuration.GetSection(ReplayOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ReplayClock>();
        services.AddSingleton<ReplayEngine>();
        services.AddHostedService(provider => provider.GetRequiredService<ReplayEngine>());
        return services;
    }
}
