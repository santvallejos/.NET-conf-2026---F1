namespace PitWall.Data.Entities;

/// <summary>Fila de la tabla <c>Interval</c>; el tipo no puede llamarse igual que su propiedad <see cref="Interval"/>.</summary>
public sealed class IntervalSample
{
    public int SessionKey { get; set; }

    public int DriverNumber { get; set; }

    public DateTimeOffset Date { get; set; }

    public double? GapToLeader { get; set; }

    public double? Interval { get; set; }
}
