namespace PitWall.Web.Components.Shared.Views;

/// <summary>
/// Vista del pit wall (docs/ui.md §3). <see cref="Route"/> es relativa a la base (<c>""</c> = <c>/</c>),
/// <see cref="Key"/> es el atajo de teclado y <see cref="Component"/> la vista autónoma (sin parámetros).
/// </summary>
public sealed record ViewEntry(string Route, char Key, string Title, Type Component);
