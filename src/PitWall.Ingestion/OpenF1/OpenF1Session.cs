namespace PitWall.Ingestion.OpenF1;

/// <summary>Fila de <c>sessions</c>: metadatos y rango de fechas de la sesión.</summary>
public sealed record OpenF1Session
{
    public int SessionKey { get; init; }

    public int MeetingKey { get; init; }

    public string? SessionName { get; init; }

    public string? CircuitShortName { get; init; }

    public string? Location { get; init; }

    public int Year { get; init; }

    public DateTimeOffset DateStart { get; init; }

    public DateTimeOffset DateEnd { get; init; }
}
