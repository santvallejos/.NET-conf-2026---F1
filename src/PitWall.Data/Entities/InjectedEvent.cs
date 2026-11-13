namespace PitWall.Data.Entities;

public sealed class InjectedEvent
{
    public int Id { get; set; }

    public int ScenarioId { get; set; }

    public int Lap { get; set; }

    public required string Type { get; set; }

    /// <summary>Parámetros del evento serializados como JSON.</summary>
    public required string Params { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
