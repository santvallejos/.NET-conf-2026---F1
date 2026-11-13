using Microsoft.AspNetCore.Components;

using PitWall.Web.Services;

namespace PitWall.Web.Components.Shared;

/// <summary>
/// Base de los paneles: se re-renderiza con cada cambio de <see cref="PitWallSession"/>. Cada panel es autónomo
/// (no recibe el estado por parámetro) para poder montarse solo en otra ruta o ventana.
/// </summary>
public abstract class PitWallPanelBase : ComponentBase, IDisposable
{
    [Inject]
    protected PitWallSession Session { get; set; } = default!;

    protected PitWallView View => Session.View;

    public void Dispose()
    {
        Session.Changed -= OnSessionChanged;
        GC.SuppressFinalize(this);
    }

    protected override void OnInitialized() => Session.Changed += OnSessionChanged;

    private void OnSessionChanged() => _ = InvokeAsync(StateHasChanged);
}
