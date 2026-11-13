using PitWall.Domain;
using PitWall.Strategy.MonteCarlo;

namespace PitWall.Tests.Strategy.MonteCarlo;

public sealed class MonteCarloSimulatorTests
{
    private const int Lap = 30;

    private static readonly IReadOnlyList<StrategyCandidate> Candidates =
    [
        MonteCarloFixtures.StayOut(),
        MonteCarloFixtures.Box(Lap, Compound.Hard, "BOX_L_HARD"),
        MonteCarloFixtures.Box(Lap, Compound.Soft, "BOX_L_SOFT"),
        new("BOX_L+1_MEDIUM", PitCall.BoxNextLap, Lap + 1, Compound.Medium, Lap + 1, Compound.Medium),
        new("BOX_L+3_HARD", PitCall.StayOut, Lap + 3, Compound.Hard, Lap + 3, Compound.Hard),
    ];

    private readonly MonteCarloSimulator _simulator = new(MonteCarloFixtures.Options());

    [Fact]
    public void Run_SameSeed_SameResult()
    {
        var input = MonteCarloFixtures.Input(Lap);

        var first = _simulator.Run(input, Candidates);
        var second = _simulator.Run(input, Candidates);

        Assert.Equal(first.Count, second.Count);
        for (var i = 0; i < first.Count; i++)
        {
            Assert.Equal(first[i].StrategyId, second[i].StrategyId);
            Assert.Equal(first[i].ExpectedPosition, second[i].ExpectedPosition);
            Assert.Equal(first[i].PGainPositions, second[i].PGainPositions);
            Assert.Equal(first[i].PLosePositions, second[i].PLosePositions);
            Assert.Equal(first[i].ExpectedRaceTimeDelta, second[i].ExpectedRaceTimeDelta);
            Assert.Equal(first[i].PositionDistribution.OrderBy(p => p.Key), second[i].PositionDistribution.OrderBy(p => p.Key));
        }
    }

    [Fact]
    public void Run_CandidateAlone_SameResultAsInBatchWithSameIndex()
    {
        // La semilla depende solo del índice de la candidata: el paralelismo no cambia nada.
        var input = MonteCarloFixtures.Input(Lap);

        var batch = _simulator.Run(input, Candidates);
        var alone = _simulator.Run(input, [Candidates[0]]);

        Assert.Equal(batch[0].ExpectedPosition, alone[0].ExpectedPosition);
        Assert.Equal(batch[0].PositionDistribution.OrderBy(p => p.Key), alone[0].PositionDistribution.OrderBy(p => p.Key));
    }

    [Fact]
    public void Run_DifferentSeed_DifferentResult()
    {
        var first = _simulator.Run(MonteCarloFixtures.Input(Lap, seed: 1), Candidates);
        var second = _simulator.Run(MonteCarloFixtures.Input(Lap, seed: 2), Candidates);

        Assert.Contains(first.Zip(second), pair => pair.First.ExpectedRaceTimeDelta != pair.Second.ExpectedRaceTimeDelta);
    }

    [Fact]
    public void Run_AnyCandidate_DistributionSumsToOneAndMatchesExpectations()
    {
        var input = MonteCarloFixtures.Input(Lap, focusPosition: 5);

        var outcomes = _simulator.Run(input, Candidates);

        Assert.Equal(Candidates.Count, outcomes.Count);
        Assert.All(outcomes, outcome =>
        {
            Assert.Equal(1.0, outcome.PositionDistribution.Values.Sum(), 9);
            Assert.All(outcome.PositionDistribution.Keys, position => Assert.InRange(position, 1, input.Cars.Count));
            Assert.Equal(outcome.PositionDistribution.Sum(p => p.Key * p.Value), outcome.ExpectedPosition, 9);
            Assert.Equal(outcome.PositionDistribution.Where(p => p.Key < 5).Sum(p => p.Value), outcome.PGainPositions, 9);
            Assert.Equal(outcome.PositionDistribution.Where(p => p.Key > 5).Sum(p => p.Value), outcome.PLosePositions, 9);
            Assert.True(outcome.ExpectedRaceTimeDelta >= 0);
        });
        Assert.Contains(outcomes, outcome => outcome.ExpectedRaceTimeDelta == 0);
    }

    [Fact]
    public void Run_Puncture_BoxNowWins()
    {
        var input = MonteCarloFixtures.Input(Lap, puncture: true);

        var outcomes = _simulator.Run(input, Candidates);

        var best = outcomes.OrderBy(o => o.ExpectedPosition).ThenBy(o => o.ExpectedRaceTimeDelta).First();
        Assert.Equal(PitCall.Box, best.Call);
        Assert.Equal(Lap, best.BoxLap);
        var stayOut = Assert.Single(outcomes, o => o.StrategyId == CandidateGenerator.StayOutId);
        Assert.True(stayOut.ExpectedPosition > best.ExpectedPosition);
        // Cada vuelta extra con pinchazo cuesta 20–40 s (el SC puede recortar parte al agrupar la parrilla).
        var boxNowHard = Assert.Single(outcomes, o => o.StrategyId == "BOX_L_HARD");
        var boxLaterHard = Assert.Single(outcomes, o => o.StrategyId == "BOX_L+3_HARD");
        Assert.True(boxLaterHard.ExpectedRaceTimeDelta > boxNowHard.ExpectedRaceTimeDelta + 20);
    }

    [Fact]
    public void Run_TyrePastCliff_BoxBeatsStayingOut()
    {
        // Goma destruida: pierde 1 s más por vuelta y lleva 30 vueltas.
        var input = MonteCarloFixtures.Input(Lap, focusTyre: MonteCarloFixtures.Tyre(Compound.Medium, age: 30, deg: 1.0, lapsToCliff: 0));

        var outcomes = _simulator.Run(input, [MonteCarloFixtures.StayOut(), MonteCarloFixtures.Box(Lap, Compound.Hard, "BOX_L_HARD")]);

        Assert.True(outcomes[1].ExpectedPosition < outcomes[0].ExpectedPosition);
        Assert.Equal(0, outcomes[1].ExpectedRaceTimeDelta);
    }

    [Fact]
    public void Run_PermanentSafetyCar_HalvesPitLoss()
    {
        // Un solo auto sin degradación ni ruido: parar cuesta exactamente el pit loss.
        var flat = MonteCarloFixtures.Tyre(Compound.Medium, age: 20, deg: 0, sigma: 0, lapsToCliff: 200);
        var input = MonteCarloFixtures.Input(Lap, cars: 1, focusPosition: 1, focusTyre: flat) with
        {
            FocusCompounds = new Dictionary<Compound, TyreModel> { [Compound.Medium] = flat with { TyreAge = 0 } },
            PitLossSigma = 0,
        };
        StrategyCandidate[] candidates = [MonteCarloFixtures.StayOut(), MonteCarloFixtures.Box(Lap, Compound.Medium, "BOX_L_MEDIUM")];
        var safetyCar = new MonteCarloSimulator(new MonteCarloOptions { Iterations = 1000, Seed = MonteCarloFixtures.Seed, ProbSafetyCarPerLap = 1.0, ProbVscPerLap = 0 });
        var green = new MonteCarloSimulator(new MonteCarloOptions { Iterations = 1000, Seed = MonteCarloFixtures.Seed, ProbSafetyCarPerLap = 0, ProbVscPerLap = 0 });

        Assert.Equal(input.PitLossMean, green.Run(input, candidates)[1].ExpectedRaceTimeDelta, 6);
        Assert.Equal(input.PitLossMean * 0.5, safetyCar.Run(input, candidates)[1].ExpectedRaceTimeDelta, 6);
    }

    [Fact]
    public void Run_CandidateWithoutPaceModel_Throws()
    {
        var input = MonteCarloFixtures.Input(Lap);

        Assert.Throws<ArgumentException>(() => _simulator.Run(input, [MonteCarloFixtures.Box(Lap, Compound.Wet, "BOX_L_WET")]));
    }
}
