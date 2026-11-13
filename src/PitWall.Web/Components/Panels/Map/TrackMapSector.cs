namespace PitWall.Web.Components.Panels.Map;

/// <summary>
/// Marca del inicio de un sector en el mapa: barra perpendicular al trazado (de <paramref name="From"/> a
/// <paramref name="To"/>) y posición de la etiqueta, del lado de afuera del circuito.
/// </summary>
public sealed record TrackMapSector(string Label, TrackPoint From, TrackPoint To, TrackPoint LabelAt);
