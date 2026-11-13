using System.Text.Json.Serialization;

namespace PitWall.Ingestion.OpenF1;

public sealed record OpenF1Interval
{
    public int DriverNumber { get; init; }

    public DateTimeOffset Date { get; init; }

    /// <summary>Segundos; <c>null</c> cuando OpenF1 publica texto (p. ej. <c>"+1 LAP"</c>).</summary>
    [JsonConverter(typeof(LenientDoubleConverter))]
    public double? GapToLeader { get; init; }

    [JsonConverter(typeof(LenientDoubleConverter))]
    public double? Interval { get; init; }
}
