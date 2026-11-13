using PitWall.Domain;

namespace PitWall.Data.Entities;

public sealed class Stint
{
    public int SessionKey { get; set; }

    public int DriverNumber { get; set; }

    public int StintNumber { get; set; }

    public int LapStart { get; set; }

    public int LapEnd { get; set; }

    public Compound Compound { get; set; }

    public int TyreAgeAtStart { get; set; }
}
