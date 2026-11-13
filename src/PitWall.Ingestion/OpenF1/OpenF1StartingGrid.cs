using System.Text.Json.Serialization;

namespace PitWall.Ingestion.OpenF1;

public sealed record OpenF1StartingGrid
{
    public int DriverNumber { get; init; }

    public int Position { get; init; }

    [JsonConverter(typeof(LenientDoubleConverter))]
    public double? LapDuration { get; init; }
}
