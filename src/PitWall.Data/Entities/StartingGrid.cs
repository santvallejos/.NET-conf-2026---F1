namespace PitWall.Data.Entities;

/// <summary>Grilla de salida; OpenF1 la expone en la sesión de clasificación, pero se guarda con la <c>SessionKey</c> de carrera.</summary>
public sealed class StartingGrid
{
    public int SessionKey { get; set; }

    public int DriverNumber { get; set; }

    public int Position { get; set; }

    public double? LapDuration { get; set; }
}
