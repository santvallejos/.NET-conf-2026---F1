namespace PitWall.Data.Entities;

/// <summary>Fila de la tabla <c>Position</c>; el tipo no puede llamarse igual que su propiedad <see cref="Position"/>.</summary>
public sealed class PositionSample
{
    public int SessionKey { get; set; }

    public int DriverNumber { get; set; }

    public DateTimeOffset Date { get; set; }

    public int Position { get; set; }
}
