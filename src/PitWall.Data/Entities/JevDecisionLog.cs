using PitWall.Domain;

namespace PitWall.Data.Entities;

public sealed class JevDecisionLog
{
    public int Id { get; set; }

    public int SessionKey { get; set; }

    public int? ScenarioId { get; set; }

    public int DriverNumber { get; set; }

    public int Lap { get; set; }

    public JevKind Kind { get; set; }

    public required string StateHash { get; set; }

    public required string RequestJson { get; set; }

    public required string ResponseJson { get; set; }

    public int LatencyMs { get; set; }

    public int Tokens { get; set; }

    public double CostUsd { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
