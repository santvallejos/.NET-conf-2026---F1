using PitWall.Domain;
using PitWall.Strategy.Degradation;

namespace PitWall.Web.Components.Panels.Tyres;

/// <summary>Lo que muestra el panel Neumáticos de un piloto en el instante actual del replay.</summary>
/// <param name="Fit">Modelo del compuesto actual; <c>null</c> si no hay vueltas limpias suficientes en la parrilla.</param>
/// <param name="CliffTyreAge">Edad de goma en la que se supera el umbral de cliff, o <see cref="TyreDegradationModel.NoCliff"/>.</param>
public sealed record TyrePanelData(
    int DriverNumber,
    int Lap,
    int LapCount,
    Compound Compound,
    int TyreAge,
    IReadOnlyList<TyreStint> Stints,
    DegradationFit? Fit,
    int CliffTyreAge,
    double CliffDeltaSeconds);
