using PitWall.Domain;
using PitWall.Strategy.MonteCarlo;
using PitWall.Strategy.Rules;

namespace PitWall.Tests.Strategy.MonteCarlo;

public sealed class StrategyCalculatorTests
{
    private const int Lap = 20;

    [Fact]
    public void Calculate_DryRace_SnapshotWithSortedOutcomesForAllowedCandidates()
    {
        var context = MonteCarloFixtures.Race(Lap);

        var result = MonteCarloFixtures.Calculator().Calculate(context, MonteCarloFixtures.Focus);

        Assert.NotNull(result);
        var snapshot = result.Snapshot;
        Assert.Equal(MonteCarloFixtures.Focus, snapshot.DriverNumber);
        Assert.Equal(Lap, snapshot.Lap);
        Assert.Equal(Compound.Medium, snapshot.Tyre.Compound);
        Assert.False(snapshot.Tyre.IsFallback);
        Assert.Contains(snapshot.Constraints, constraint => constraint.Rule == ConstraintRules.DryTrack);
        // STAY_OUT + 4 vueltas × SOFT/MEDIUM (propios) y HARD (media de la parrilla); sin modelo de lluvia.
        Assert.Equal(13, snapshot.Outcomes.Count);
        Assert.Contains(snapshot.Outcomes, outcome => outcome.StrategyId == CandidateGenerator.StayOutId);
        Assert.Contains(snapshot.Outcomes, outcome => outcome.StrategyId == "BOX_L+2_HARD");
        Assert.DoesNotContain(snapshot.Outcomes, outcome => outcome.Compound is Compound.Wet or Compound.Intermediate);
        Assert.Equal(snapshot.Outcomes.OrderBy(o => o.ExpectedPosition).Select(o => o.StrategyId), snapshot.Outcomes.Select(o => o.StrategyId));
        Assert.All(snapshot.Outcomes, outcome => Assert.Equal(1.0, outcome.PositionDistribution.Values.Sum(), 9));
        Assert.Equal(1000, result.Iterations);
    }

    [Fact]
    public void Calculate_SameContextTwice_SameSnapshot()
    {
        var calculator = MonteCarloFixtures.Calculator();
        var context = MonteCarloFixtures.Race(Lap);

        var first = calculator.Calculate(context, MonteCarloFixtures.Focus)!.Snapshot;
        var second = calculator.Calculate(context, MonteCarloFixtures.Focus)!.Snapshot;

        Assert.Equal(first.Outcomes.Select(o => (o.StrategyId, o.ExpectedPosition, o.PGainPositions, o.ExpectedRaceTimeDelta)),
            second.Outcomes.Select(o => (o.StrategyId, o.ExpectedPosition, o.PGainPositions, o.ExpectedRaceTimeDelta)));
    }

    [Fact]
    public void Calculate_InjectedPuncture_OnlyBoxNowAndPunctureReason()
    {
        var context = MonteCarloFixtures.Race(Lap, punctured: new HashSet<int> { MonteCarloFixtures.Focus });

        var snapshot = MonteCarloFixtures.Calculator().Calculate(context, MonteCarloFixtures.Focus)!.Snapshot;

        Assert.Contains(Reason.Puncture, snapshot.Reasons);
        Assert.Contains(snapshot.Constraints, constraint => constraint.Rule == ConstraintRules.Puncture);
        Assert.NotEmpty(snapshot.Outcomes);
        Assert.All(snapshot.Outcomes, outcome =>
        {
            Assert.Equal(PitCall.Box, outcome.Call);
            Assert.Equal(Lap, outcome.BoxLap);
        });
    }

    [Fact]
    public void Calculate_DriverNotInRace_ReturnsNull()
    {
        var result = MonteCarloFixtures.Calculator().Calculate(MonteCarloFixtures.Race(Lap), driverNumber: 99);

        Assert.Null(result);
    }
}
