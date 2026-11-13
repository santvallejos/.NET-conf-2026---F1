namespace PitWall.Web.Components.Shared;

/// <summary>
/// Panel montable en una zona del layout de charla o solo en <c>/panel/{Slug}</c>. <see cref="Components"/> son
/// paneles autónomos (sin parámetros) que se renderizan en orden.
/// </summary>
public sealed record PanelEntry(string Slug, string Title, IReadOnlyList<Type> Components);
