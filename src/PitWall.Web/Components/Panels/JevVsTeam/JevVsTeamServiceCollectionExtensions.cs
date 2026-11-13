namespace PitWall.Web.Components.Panels.JevVsTeam;

public static class JevVsTeamServiceCollectionExtensions
{
    /// <summary>Registra el servicio de la decisión real del equipo del panel Jev vs equipo. Requiere <c>AddPitWallStorage</c>.</summary>
    public static IServiceCollection AddPitWallJevVsTeam(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<TeamDecisionService>();
        return services;
    }
}
