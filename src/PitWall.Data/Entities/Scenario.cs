namespace PitWall.Data.Entities;

public sealed class Scenario
{
    public int Id { get; set; }

    public int SessionKey { get; set; }

    public required string Name { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
