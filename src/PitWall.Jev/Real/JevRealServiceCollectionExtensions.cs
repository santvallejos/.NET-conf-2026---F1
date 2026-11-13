using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using PitWall.Jev.Guards;

namespace PitWall.Jev.Real;

public static class JevRealServiceCollectionExtensions
{
    /// <summary>
    /// Registra <see cref="JevClient"/> como cliente con clave <see cref="JevClientKind.Real"/>, su <c>HttpClient</c> con
    /// nombre sobre <c>Jev:BaseUrl</c> y el apagado de Jev cuando falta <c>Jev:ApiKey</c>. Se llama después de
    /// <c>AddPitWallJev</c>, para que el apagado corra después de los overrides en caliente.
    /// </summary>
    /// <param name="alertSessionKey">Sesión en la que se muestra la alerta de key ausente al arrancar.</param>
    public static IServiceCollection AddPitWallJevReal(
        this IServiceCollection services,
        Func<IServiceProvider, int?> alertSessionKey)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(alertSessionKey);

        services.AddHttpClient(JevClient.HttpClientName, (sp, http) =>
            http.BaseAddress = BaseAddress(sp.GetRequiredService<IOptions<JevOptions>>().Value.BaseUrl));

        services.TryAddSingleton(TimeProvider.System);
        services.RemoveAllKeyed<IJevClient>(JevClientKind.Real);
        services.AddKeyedSingleton<IJevClient, JevClient>(JevClientKind.Real);

        services.AddSingleton<IPostConfigureOptions<JevOptions>, JevApiKeyPostConfigure>();
        services.AddHostedService(sp => new JevApiKeyStartupCheck(
            sp.GetRequiredService<IOptionsMonitor<JevOptions>>(),
            sp.GetRequiredService<JevAlerts>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILogger<JevApiKeyStartupCheck>>(),
            alertSessionKey(sp)));

        return services;
    }

    // Con barra final, "systemone" se resuelve como {BaseUrl}systemone y no reemplaza el último segmento (/v1).
    private static Uri BaseAddress(string baseUrl) =>
        new(baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/", UriKind.Absolute);
}
