namespace PitWall.Data.Entities;

/// <summary>Sesión descargada por la CLI de ingesta; la UI lista las sesiones disponibles desde esta tabla.</summary>
public sealed class IngestedSession
{
    public int SessionKey { get; set; }

    public int MeetingKey { get; set; }

    public required string Name { get; set; }

    public required string Circuit { get; set; }

    public DateTimeOffset DateStart { get; set; }

    public DateTimeOffset DateEnd { get; set; }

    public int? QualiSessionKey { get; set; }

    public DateTimeOffset IngestedAt { get; set; }
}
