using System.Text.Json.Serialization;

namespace PitWall.Ingestion.OpenF1;

public sealed record OpenF1Lap
{
    public int DriverNumber { get; init; }

    public int LapNumber { get; init; }

    public DateTimeOffset? DateStart { get; init; }

    public double? LapDuration { get; init; }

    [JsonPropertyName("duration_sector_1")]
    public double? DurationSector1 { get; init; }

    [JsonPropertyName("duration_sector_2")]
    public double? DurationSector2 { get; init; }

    [JsonPropertyName("duration_sector_3")]
    public double? DurationSector3 { get; init; }

    [JsonPropertyName("i1_speed")]
    public int? I1Speed { get; init; }

    [JsonPropertyName("i2_speed")]
    public int? I2Speed { get; init; }

    public bool? IsPitOutLap { get; init; }
}
