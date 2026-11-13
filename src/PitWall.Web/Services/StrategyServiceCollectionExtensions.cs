using Microsoft.Extensions.Options;

using PitWall.Strategy.Degradation;
using PitWall.Strategy.MonteCarlo;
using PitWall.Strategy.Rules;
using PitWall.Strategy.Workers;

namespace PitWall.Web.Services;

public static class StrategyServiceCollectionExtensions
{
    /// <summary>
    /// Registra <see cref="MonteCarloOptions"/> (<c>Strategy:MonteCarlo</c>), <see cref="RulesOptions"/>
    /// (<c>Strategy:Rules</c>), el motor de reglas, Monte Carlo, el cálculo por piloto y el worker que lo corre al
    /// cerrar cada vuelta. Requiere <c>AddPitWallUi</c>, <c>AddPitWallTyres</c> y <c>AddPitWallEnergy</c>.
    /// </summary>
    public static IServiceCollection AddPitWallStrategy(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<MonteCarloOptions>()
            .Bind(configuration.GetSection(MonteCarloOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => options.PunctureLossMinSeconds <= options.PunctureLossMaxSeconds, "PunctureLossMinSeconds must not exceed PunctureLossMaxSeconds.")
            .ValidateOnStart();
        services.AddOptions<RulesOptions>()
            .Bind(configuration.GetSection(RulesOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(provider => new RulesEngine(provider.GetRequiredService<IOptions<RulesOptions>>().Value));
        services.AddSingleton(provider => new MonteCarloSimulator(provider.GetRequiredService<IOptions<MonteCarloOptions>>().Value));
        services.AddSingleton(provider => new StrategyCalculator(
            provider.GetRequiredService<TyreDegradationModel>(),
            provider.GetRequiredService<IOptions<DegradationOptions>>().Value,
            provider.GetRequiredService<RulesEngine>(),
            provider.GetRequiredService<MonteCarloSimulator>(),
            provider.GetRequiredService<IOptions<MonteCarloOptions>>().Value,
            provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<StrategyStore>();
        services.AddSingleton<StrategyMetrics>();
        services.AddHostedService<StrategyWorker>();
        return services;
    }
}
