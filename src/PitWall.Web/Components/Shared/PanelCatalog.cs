using PitWall.Web.Components.Panels.Controls;
using PitWall.Web.Components.Panels.Energy;
using PitWall.Web.Components.Panels.Jev;
using PitWall.Web.Components.Panels.JevVsTeam;
using PitWall.Web.Components.Panels.Map;
using PitWall.Web.Components.Panels.MonteCarlo;
using PitWall.Web.Components.Panels.Radio;
using PitWall.Web.Components.Panels.RaceControl;
using PitWall.Web.Components.Panels.Scenarios;
using PitWall.Web.Components.Panels.Telemetry;
using PitWall.Web.Components.Panels.Tower;
using PitWall.Web.Components.Panels.Tyres;
using PitWall.Web.Components.Panels.Weather;

namespace PitWall.Web.Components.Shared;

/// <summary>
/// Paneles del pit wall (docs/ui.md §2): los usa la grilla de la charla y las rutas <c>/panel/{slug}</c> del ensayo
/// multi-monitor. Todos leen el mismo <see cref="Services.PitWallSession"/>, así que cada ventana ve el mismo estado.
/// </summary>
public static class PanelCatalog
{
    public static PanelEntry Tower { get; } = new("tower", "Torre de tiempos", [typeof(TimingTower)]);

    public static PanelEntry Map { get; } = new("map", "Mapa", [typeof(TrackMap)]);

    public static PanelEntry Telemetry { get; } = new("telemetry", "Telemetría", [typeof(TelemetryChart)]);

    public static PanelEntry Tyres { get; } = new("tyres", "Neumáticos", [typeof(TyrePanel)]);

    public static PanelEntry MonteCarlo { get; } = new("montecarlo", "Monte Carlo", [typeof(MonteCarloPanel)]);

    public static PanelEntry Weather { get; } = new("weather", "Clima", [typeof(WeatherWidget)]);

    public static PanelEntry RaceControl { get; } = new("race-control", "Race Control", [typeof(RaceControlFeed)]);

    public static PanelEntry Energy { get; } = new("energy", "Energía", [typeof(EnergyWidget)]);

    public static PanelEntry Jev { get; } = new("jev", "Jev", [typeof(JevPanel)]);

    public static PanelEntry JevVsTeam { get; } = new("jev-vs-team", "Jev vs equipo", [typeof(JevVsTeamPanel)]);

    public static PanelEntry Controls { get; } = new("controls", "Controles", [typeof(FlagBar), typeof(ReplayControls)]);

    public static PanelEntry Scenarios { get; } =
        new("scenarios", "Escenarios", [typeof(ScenarioInjector), typeof(ScenarioTimeline)]);

    public static PanelEntry Radio { get; } = new("radio", "Radio", [typeof(RadioPlayer)]);

    public static IReadOnlyList<PanelEntry> All { get; } =
        [Tower, Map, Telemetry, Tyres, MonteCarlo, Weather, RaceControl, Energy, Jev, JevVsTeam, Controls, Scenarios, Radio];

    public static PanelEntry? Find(string? slug) =>
        All.FirstOrDefault(entry => string.Equals(entry.Slug, slug, StringComparison.OrdinalIgnoreCase));
}
