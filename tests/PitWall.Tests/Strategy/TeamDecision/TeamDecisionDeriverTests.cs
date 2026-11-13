using PitWall.Domain;
using PitWall.Strategy.TeamDecision;

namespace PitWall.Tests.Strategy.TeamDecision;

public sealed class TeamDecisionDeriverTests
{
    private const int Driver = 43;

    private static readonly TeamStint[] TwoStints =
    [
        new(Driver, 1, 1, 5, Compound.Intermediate),
        new(Driver, 2, 6, 10, Compound.Soft),
    ];

    [Fact]
    public void Derive_PitLap_ReturnsBoxWithNextStintCompound()
    {
        var decisions = TeamDecisionDeriver.Derive(Driver, TwoStints, [new TeamPitStop(Driver, 5)]);

        var box = Assert.Single(decisions, decision => decision.PitCall == PitCall.Box);
        Assert.Equal(new TeamLapDecision(Driver, 5, PitCall.Box, Compound.Soft), box);
    }

    [Fact]
    public void Derive_LapsWithoutPit_ReturnStayOutWithoutCompound()
    {
        var decisions = TeamDecisionDeriver.Derive(Driver, TwoStints, [new TeamPitStop(Driver, 5)]);

        Assert.Equal(Enumerable.Range(1, 10), decisions.Select(decision => decision.Lap));
        Assert.All(
            decisions.Where(decision => decision.Lap != 5),
            decision => Assert.Equal(new TeamLapDecision(Driver, decision.Lap, PitCall.StayOut, null), decision));
    }

    [Fact]
    public void Derive_PitWithoutFollowingStint_ReturnsBoxWithoutCompound()
    {
        var decisions = TeamDecisionDeriver.Derive(Driver, TwoStints, [new TeamPitStop(Driver, 10)]);

        Assert.Equal(new TeamLapDecision(Driver, 10, PitCall.Box, null), decisions[^1]);
    }

    [Fact]
    public void Derive_DuplicatedPitRows_ReturnsOneDecisionPerLap()
    {
        var decisions = TeamDecisionDeriver.Derive(
            Driver,
            TwoStints,
            [new TeamPitStop(Driver, 5), new TeamPitStop(Driver, 5)]);

        Assert.Equal(10, decisions.Count);
        Assert.Single(decisions, decision => decision.PitCall == PitCall.Box);
    }

    [Fact]
    public void Derive_OtherDriversRows_AreIgnored()
    {
        TeamStint[] stints = [.. TwoStints, new(12, 3, 11, 20, Compound.Hard)];

        var decisions = TeamDecisionDeriver.Derive(Driver, stints, [new TeamPitStop(12, 8)]);

        Assert.Equal(10, decisions.Count);
        Assert.All(decisions, decision => Assert.Equal(PitCall.StayOut, decision.PitCall));
    }

    [Fact]
    public void Derive_DriverWithoutData_ReturnsEmpty()
    {
        var decisions = TeamDecisionDeriver.Derive(99, TwoStints, [new TeamPitStop(Driver, 5)]);

        Assert.Empty(decisions);
    }
}
