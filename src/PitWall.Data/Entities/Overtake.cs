namespace PitWall.Data.Entities;

public sealed class Overtake
{
    public int SessionKey { get; set; }

    public DateTimeOffset Date { get; set; }

    public int OvertakingDriver { get; set; }

    public int OvertakenDriver { get; set; }

    public int Position { get; set; }
}
