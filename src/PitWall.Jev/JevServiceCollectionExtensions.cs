using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using PitWall.Jev.Caching;
using PitWall.Jev.Fake;
using PitWall.Jev.Guards;

namespace PitWall.Jev;

public static class JevServiceCollectionExtensions
{
    /// <summary>
    /// Registra <see cref="JevOptions"/> y <see cref="IJevClient"/> como <see cref="GuardedJevClient"/> sobre el
    /// cliente con clave <c>Jev:Client</c>. El cliente real se registra aparte como servicio con clave
    /// <see cref="JevClientKind.Real"/>; mientras no exista, cada llamada falla con alerta.
    /// </summary>
    public static IServiceCollection AddPitWallJev(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<JevOptions>()
            .BindConfiguration(JevOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IJevCacheStore, InMemoryJevCacheStore>();
        services.TryAddSingleton<JevMetrics>();
        services.TryAddSingleton<JevAlerts>();
        services.TryAddKeyedSingleton<IJevClient, FakeJevClient>(JevClientKind.Fake);
        services.TryAddKeyedSingleton<IJevClient, UnavailableJevClient>(JevClientKind.Real);

        services.TryAddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptionsMonitor<JevOptions>>();
            var clientKind = options.CurrentValue.Client;

            return new GuardedJevClient(
                sp.GetRequiredKeyedService<IJevClient>(clientKind),
                clientKind,
                sp.GetRequiredService<IJevCacheStore>(),
                options,
                sp.GetRequiredService<JevMetrics>(),
                sp.GetRequiredService<JevAlerts>(),
                sp.GetRequiredService<TimeProvider>(),
                sp.GetRequiredService<ILogger<GuardedJevClient>>());
        });
        services.TryAddSingleton<IJevClient>(sp => sp.GetRequiredService<GuardedJevClient>());

        return services;
    }
}
