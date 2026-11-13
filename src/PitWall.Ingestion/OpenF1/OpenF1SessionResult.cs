using System.Text.Json.Serialization;

namespace PitWall.Ingestion.OpenF1;

public sealed record OpenF1SessionResult
{
    public int DriverNumber { get; init; }

    public int? Position { get; init; }

    public int? NumberOfLaps { get; init; }

    public bool? Dnf { get; init; }

    public bool? Dns { get; init; }

    public bool? Dsq { get; init; }

    /// <summary>Segundos; <c>null</c> si no terminó o si OpenF1 publica otra forma (lista en clasificación).</summary>
    [JsonConverter(typeof(LenientDoubleConverter))]
    public double? Duration { get; init; }

    /// <summary>Segundos; <c>null</c> cuando OpenF1 publica texto (p. ej. <c>"+1 LAP"</c>).</summary>
    [JsonConverter(typeof(LenientDoubleConverter))]
    public double? GapToLeader { get; init; }
}
