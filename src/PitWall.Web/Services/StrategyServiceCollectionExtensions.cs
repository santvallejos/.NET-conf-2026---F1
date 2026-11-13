using Microsoft.Extensions.Options;

using PitWall.Strategy.Degradation;
using PitWall.Strategy.PitWindow;
using PitWall.Strategy.Workers;

namespace PitWall.Web.Services;

public static class StrategyServiceCollectionExtensions
{
    /// <summary>
    /// Registra <see cref="PitCallOptions"/> (<c>Strategy:PitCall</c>), el cálculo por piloto y el worker que lo corre
    /// al cerrar cada vuelta. Requiere <c>AddPitWallUi</c>, <c>AddPitWallTyres</c> y <c>AddPitWallEnergy</c>.
    /// </summary>
    public static IServiceCollection AddPitWallStrategy(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<PitCallOptions>()
            .Bind(configuration.GetSection(PitCallOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(provider => new StrategyCalculator(
            provider.GetRequiredService<TyreDegradationModel>(),
            provider.GetRequiredService<IOptions<DegradationOptions>>().Value,
            provider.GetRequiredService<IOptions<PitCallOptions>>().Value,
            provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<StrategyStore>();
        services.AddHostedService<StrategyWorker>();
        return services;
    }
}
