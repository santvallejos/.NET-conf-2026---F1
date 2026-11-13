using PitWall.Domain;

namespace PitWall.Data.Entities;

public sealed class JevCache
{
    public required string StateHash { get; set; }

    public JevKind Kind { get; set; }

    public required string ResponseJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
