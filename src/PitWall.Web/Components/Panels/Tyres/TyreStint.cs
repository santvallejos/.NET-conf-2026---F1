using PitWall.Domain;

namespace PitWall.Web.Components.Panels.Tyres;

/// <summary>Stint de un piloto hasta la vuelta actual; <see cref="LapEnd"/> es la última vuelta ya iniciada.</summary>
public sealed record TyreStint(int StintNumber, Compound Compound, int LapStart, int LapEnd, int TyreAgeAtStart)
{
    public int Laps => LapEnd - LapStart + 1;
}
