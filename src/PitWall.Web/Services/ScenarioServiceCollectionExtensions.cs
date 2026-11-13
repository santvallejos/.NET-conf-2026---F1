using PitWall.Strategy.Scenarios;

namespace PitWall.Web.Services;

public static class ScenarioServiceCollectionExtensions
{
    /// <summary>
    /// Registra <see cref="ScenarioOptions"/> (<c>Scenarios</c>), los escenarios de la sesión y la estrategia hipotética.
    /// Requiere <c>AddPitWallStorage</c> (<see cref="IScenarioStore"/>) y <c>AddPitWallUi</c>.
    /// </summary>
    public static IServiceCollection AddPitWallScenarios(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<ScenarioOptions>()
            .Bind(configuration.GetSection(ScenarioOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => options.HasValidPunctureLoss(), "Scenarios:PunctureLossSeconds must be empty or [min, max] with 0 <= min <= max <= 300.")
            .ValidateOnStart();
        services.AddSingleton<ScenarioService>();
        services.AddSingleton<ScenarioStrategyStore>();
        return services;
    }
}
