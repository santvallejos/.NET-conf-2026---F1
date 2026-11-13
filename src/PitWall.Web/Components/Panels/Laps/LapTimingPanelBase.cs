using Microsoft.AspNetCore.Components;

using PitWall.Strategy.Timing;
using PitWall.Web.Components.Shared;

namespace PitWall.Web.Components.Panels.Laps;

/// <summary>
/// Base de los paneles que leen el timing por vuelta (torre, mejores sectores, ritmo de carrera). Cualquiera de
/// ellos puede estar solo en su ventana, así que cada uno carga las vueltas la primera vez que ve una sesión nueva.
/// </summary>
public abstract class LapTimingPanelBase : PitWallPanelBase
{
    private int? _loadingSession;

    [Inject]
    protected LapTimingService LapTiming { get; set; } = default!;

    /// <summary>Timing al instante del replay; <c>null</c> mientras se cargan las vueltas o sin sesión.</summary>
    protected LapTimingSnapshot? Timing => LapTiming.Get(View);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // La sesión cambia desde los controles: las vueltas se cargan la primera vez que el panel la ve.
        if (View.State is not { } state || LapTiming.IsLoaded(state.SessionKey) || _loadingSession == state.SessionKey)
        {
            return;
        }

        _loadingSession = state.SessionKey;
        await LapTiming.LoadAsync(state.SessionKey);
        _loadingSession = null;
        StateHasChanged();
    }
}
