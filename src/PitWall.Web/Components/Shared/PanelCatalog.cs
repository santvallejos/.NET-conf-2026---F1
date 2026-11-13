using PitWall.Web.Components.Panels.BestSectors;
using PitWall.Web.Components.Panels.Controls;
using PitWall.Web.Components.Panels.Energy;
using PitWall.Web.Components.Panels.Map;
using PitWall.Web.Components.Panels.PitWindow;
using PitWall.Web.Components.Panels.Radio;
using PitWall.Web.Components.Panels.RaceControl;
using PitWall.Web.Components.Panels.RacePace;
using PitWall.Web.Components.Panels.Telemetry;
using PitWall.Web.Components.Panels.Tower;
using PitWall.Web.Components.Panels.Tyres;
using PitWall.Web.Components.Panels.Weather;

namespace PitWall.Web.Components.Shared;

/// <summary>
/// Paneles (docs/ui.md §2): los usan la grilla de la charla y las rutas <c>/panel/{slug}</c> del ensayo
/// multi-monitor. Todos leen el mismo <see cref="Services.PitWallSession"/>, así que cada ventana ve el mismo estado.
/// </summary>
public static class PanelCatalog
{
    public static PanelEntry Tower { get; } = new("tower", "Torre de tiempos", [typeof(TimingTower)]);

    public static PanelEntry Map { get; } = new("map", "Mapa", [typeof(TrackMap)]);

    public static PanelEntry Telemetry { get; } = new("telemetry", "Telemetría", [typeof(TelemetryChart)]);

    public static PanelEntry Tyres { get; } = new("tyres", "Neumáticos", [typeof(TyrePanel)]);

    public static PanelEntry Weather { get; } = new("weather", "Clima", [typeof(WeatherWidget)]);

    /// <summary>Clima ampliado de la vista Condiciones (valores, delta vs inicio y series).</summary>
    public static PanelEntry WeatherDetail { get; } = new("weather-detail", "Clima ampliado", [typeof(WeatherDetail)]);

    public static PanelEntry RaceControl { get; } = new("race-control", "Race Control", [typeof(RaceControlFeed)]);

    public static PanelEntry Energy { get; } = new("energy", "Energía", [typeof(EnergyWidget)]);

    public static PanelEntry BestSectors { get; } = new("best-sectors", "Mejores sectores", [typeof(BestSectorsPanel)]);

    public static PanelEntry RacePace { get; } = new("race-pace", "Ritmo de carrera", [typeof(RacePacePanel)]);

    public static PanelEntry PitCall { get; } = new("pit-call", "Llamada a boxes", [typeof(PitCallPanel)]);

    public static PanelEntry Controls { get; } = new("controls", "Controles", [typeof(FlagBar), typeof(ReplayControls)]);

    public static PanelEntry Radio { get; } = new("radio", "Radio", [typeof(RadioPlayer)]);

    public static IReadOnlyList<PanelEntry> All { get; } =
        [Tower, Map, Telemetry, Tyres, Weather, WeatherDetail, RaceControl, Energy, BestSectors, RacePace, PitCall, Controls, Radio];

    public static PanelEntry? Find(string? slug) =>
        All.FirstOrDefault(entry => string.Equals(entry.Slug, slug, StringComparison.OrdinalIgnoreCase));
}
