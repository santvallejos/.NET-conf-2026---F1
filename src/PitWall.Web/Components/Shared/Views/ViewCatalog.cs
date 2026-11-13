namespace PitWall.Web.Components.Shared.Views;

/// <summary>
/// Las 4 vistas de la charla (docs/ui.md §3), en el orden de sus pestañas y atajos 1–4. No hay vista de resumen:
/// <c>/</c> redirige a <see cref="Timing"/>. Todas leen el mismo
/// <see cref="Services.PitWallSession"/>: cambiar de vista no recalcula nada.
/// </summary>
public static class ViewCatalog
{
    public static ViewEntry Timing { get; } = new("timing", '1', "Timing", typeof(TimingView));

    public static ViewEntry Gps { get; } = new("gps", '2', "GPS", typeof(GpsView));

    public static ViewEntry Car { get; } = new("car", '3', "Auto", typeof(CarView));

    public static ViewEntry Conditions { get; } = new("conditions", '4', "Condiciones", typeof(ConditionsView));

    public static IReadOnlyList<ViewEntry> All { get; } = [Timing, Gps, Car, Conditions];

    public static ViewEntry? Find(string? route) =>
        All.FirstOrDefault(entry => string.Equals(entry.Route, route, StringComparison.OrdinalIgnoreCase));
}
