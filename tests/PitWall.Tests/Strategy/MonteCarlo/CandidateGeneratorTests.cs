using PitWall.Domain;
using PitWall.Strategy.MonteCarlo;
using PitWall.Strategy.Rules;
using PitWall.Tests.Strategy.Rules;

namespace PitWall.Tests.Strategy.MonteCarlo;

public sealed class CandidateGeneratorTests
{
    private const int Lap = 30;

    private static readonly IReadOnlyDictionary<Compound, TyreModel> AllCompounds = new Dictionary<Compound, TyreModel>
    {
        [Compound.Soft] = MonteCarloFixtures.Tyre(Compound.Soft, 0),
        [Compound.Medium] = MonteCarloFixtures.Tyre(Compound.Medium, 0),
        [Compound.Hard] = MonteCarloFixtures.Tyre(Compound.Hard, 0),
        [Compound.Intermediate] = MonteCarloFixtures.Tyre(Compound.Intermediate, 0, baseLapTime: 78),
        [Compound.Wet] = MonteCarloFixtures.Tyre(Compound.Wet, 0, baseLapTime: 82),
    };

    private readonly RulesEngine _rules = new(new RulesOptions());

    [Fact]
    public void Generate_DryRace_StayOutAndBoxInWindowTimesAllowedCompounds()
    {
        var constraints = _rules.EvaluateConstraints(RuleFeaturesFactory.Neutral());

        var candidates = CandidateGenerator.Generate(Lap, MonteCarloFixtures.TotalLaps, MonteCarloFixtures.Tyre(Compound.Hard, 5), AllCompounds, constraints);

        // Pista seca: WET descartado; 4 vueltas × SOFT/MEDIUM/HARD/INTERMEDIATE + STAY_OUT.
        Assert.Equal(17, candidates.Count);
        Assert.Equal(CandidateGenerator.StayOutId, candidates[0].Id);
        Assert.DoesNotContain(candidates, candidate => candidate.Compound == Compound.Wet);
        Assert.Equal(new[] { Lap, Lap + 1, Lap + 2, Lap + 3 }, candidates.Where(c => c.BoxLap is not null).Select(c => c.BoxLap!.Value).Distinct().Order());
        var boxNow = Assert.Single(candidates, c => c.Id == "BOX_L_SOFT");
        Assert.Equal(PitCall.Box, boxNow.Call);
        Assert.Equal(Lap, boxNow.StopLap);
        Assert.Equal(PitCall.BoxNextLap, Assert.Single(candidates, c => c.Id == "BOX_L+1_INTERMEDIATE").Call);
        Assert.Equal(PitCall.StayOut, Assert.Single(candidates, c => c.Id == "BOX_L+3_HARD").Call);
        Assert.Equal(candidates.Count, candidates.Select(c => c.Id).Distinct().Count());
    }

    [Fact]
    public void Generate_Puncture_OnlyBoxNow()
    {
        var constraints = _rules.EvaluateConstraints(RuleFeaturesFactory.Neutral() with { Puncture = true });

        var candidates = CandidateGenerator.Generate(Lap, MonteCarloFixtures.TotalLaps, MonteCarloFixtures.Tyre(Compound.Hard, 5), AllCompounds, constraints);

        Assert.NotEmpty(candidates);
        Assert.All(candidates, candidate =>
        {
            Assert.Equal(PitCall.Box, candidate.Call);
            Assert.Equal(Lap, candidate.BoxLap);
        });
    }

    [Fact]
    public void Generate_RedFlag_NoBoxNow()
    {
        var constraints = _rules.EvaluateConstraints(RuleFeaturesFactory.Neutral() with { Track = new TrackStatus("RED", false, false) });

        var candidates = CandidateGenerator.Generate(Lap, MonteCarloFixtures.TotalLaps, MonteCarloFixtures.Tyre(Compound.Hard, 5), AllCompounds, constraints);

        Assert.DoesNotContain(candidates, candidate => candidate.Call == PitCall.Box);
        Assert.Contains(candidates, candidate => candidate.Id == CandidateGenerator.StayOutId);
        Assert.Contains(candidates, candidate => candidate.Call == PitCall.BoxNextLap);
    }

    [Fact]
    public void Generate_SecondDryCompoundPending_ExcludesSameDryCompound()
    {
        var constraints = _rules.EvaluateConstraints(RuleFeaturesFactory.Neutral() with { UsedCompounds = [Compound.Hard] });

        var candidates = CandidateGenerator.Generate(Lap, MonteCarloFixtures.TotalLaps, MonteCarloFixtures.Tyre(Compound.Hard, 5, lapsToCliff: 200), AllCompounds, constraints);

        Assert.DoesNotContain(candidates, candidate => candidate.Compound == Compound.Hard);
        Assert.Contains(candidates, candidate => candidate.Compound == Compound.Medium);
        // Sin cliff antes del final, STAY_OUT igual tiene que montar otro compuesto seco antes de terminar.
        var stayOut = Assert.Single(candidates, c => c.Id == CandidateGenerator.StayOutId);
        Assert.Equal(MonteCarloFixtures.TotalLaps - 1, stayOut.StopLap);
        Assert.NotEqual(Compound.Hard, stayOut.StopCompound);
    }

    [Fact]
    public void Generate_CliffBeforeEnd_StayOutStopsAtCliffWithBestCompound()
    {
        var constraints = _rules.EvaluateConstraints(RuleFeaturesFactory.Neutral());

        var candidates = CandidateGenerator.Generate(Lap, MonteCarloFixtures.TotalLaps, MonteCarloFixtures.Tyre(Compound.Hard, 5, lapsToCliff: 10), AllCompounds, constraints);

        var stayOut = Assert.Single(candidates, c => c.Id == CandidateGenerator.StayOutId);
        Assert.Null(stayOut.BoxLap);
        Assert.Equal(Lap + 10, stayOut.StopLap);
        Assert.NotNull(stayOut.StopCompound);
    }

    [Fact]
    public void Generate_NearRaceEnd_WindowEndsBeforeLastLap()
    {
        var constraints = _rules.EvaluateConstraints(RuleFeaturesFactory.Neutral());
        var lap = MonteCarloFixtures.TotalLaps - 2;

        var candidates = CandidateGenerator.Generate(lap, MonteCarloFixtures.TotalLaps, MonteCarloFixtures.Tyre(Compound.Hard, 5), AllCompounds, constraints);

        Assert.Equal(new[] { lap, lap + 1 }, candidates.Where(c => c.BoxLap is not null).Select(c => c.BoxLap!.Value).Distinct().Order());
    }

    [Fact]
    public void Generate_CompoundWithoutModel_IsNotACandidate()
    {
        var constraints = _rules.EvaluateConstraints(RuleFeaturesFactory.Neutral());
        var compounds = new Dictionary<Compound, TyreModel> { [Compound.Hard] = MonteCarloFixtures.Tyre(Compound.Hard, 0) };

        var candidates = CandidateGenerator.Generate(Lap, MonteCarloFixtures.TotalLaps, MonteCarloFixtures.Tyre(Compound.Hard, 5), compounds, constraints);

        Assert.All(candidates.Where(c => c.Compound is not null), candidate => Assert.Equal(Compound.Hard, candidate.Compound));
        Assert.Equal(5, candidates.Count);
    }
}
