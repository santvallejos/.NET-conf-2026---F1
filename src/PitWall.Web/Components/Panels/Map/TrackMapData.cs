namespace PitWall.Web.Components.Panels.Map;

/// <summary>
/// Mapa del circuito en el instante del replay: tamaño del SVG, trazado (atributo <c>points</c> de una polyline),
/// marcas de inicio de sector (S1 = largada) y los autos con <c>location</c> ingerida.
/// </summary>
public sealed record TrackMapData(
    double Width,
    double Height,
    string Outline,
    IReadOnlyList<TrackMapSector> Sectors,
    IReadOnlyList<TrackMapCar> Cars);
