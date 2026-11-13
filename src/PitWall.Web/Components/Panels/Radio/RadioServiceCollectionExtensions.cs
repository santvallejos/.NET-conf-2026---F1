namespace PitWall.Web.Components.Panels.Radio;

public static class RadioServiceCollectionExtensions
{
    /// <summary>
    /// Registra la biblioteca de radio (mp3 locales bajo <c>PitWall:RadioPath</c>). Requiere <c>AddPitWallStorage</c>
    /// y <c>AddPitWallUi</c>; los mp3 se sirven con <c>MapPitWallRadio</c>.
    /// </summary>
    public static IServiceCollection AddPitWallRadio(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<RadioLibrary>();
        return services;
    }
}
