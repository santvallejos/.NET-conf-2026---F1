using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using PitWall.Domain;
using PitWall.Jev;
using PitWall.Jev.Caching;
using PitWall.Jev.Decisions;
using PitWall.Jev.Guards;
using PitWall.Jev.Requests;
using PitWall.Jev.Serialization;

namespace PitWall.Tests.Jev.Hypothetical;

/// <summary>
/// "Consultar Jev" con el escenario <c>Real</c> y con uno hipotético: estrategia con la que se arma el request,
/// clave de caché y marca HIPOTÉTICO de la decisión. Mismo armado que el worker de Jev del host.
/// </summary>
public sealed class JevHypotheticalConsultTests : IDisposable
{
    private const string SafetyCar = "SAFETY_CAR L60 4 LAPS";

    private static readonly Guid ScenarioId = new("0193a0b4-0000-7000-8000-0000000000aa");

    private readonly FakeTimeProvider _time = new(JevTestRace.RaceStart);
    private readonly TestMeterFactory _meterFactory = new();
    private readonly StaticOptionsMonitor<JevOptions> _options = new(new JevOptions());
    private readonly CountingJevClient _inner;
    private readonly GuardedJevClient _client;

    public JevHypotheticalConsultTests()
    {
        _inner = new CountingJevClient(_time);
        _client = new GuardedJevClient(
            _inner,
            JevClientKind.Fake,
            new InMemoryJevCacheStore(),
            _options,
            new JevMetrics(_meterFactory),
            new JevAlerts(),
            _time,
            NullLogger<GuardedJevClient>.Instance);
    }

    public void Dispose() => _meterFactory.Dispose();

    [Fact]
    public void Select_HypotheticalScenarioWithStrategy_UsesScenarioStrategy()
    {
        var source = JevStrategySource.Select(Real(), Hypothetical(), hypotheticalScenario: true, scenarioHasActiveEvents: true);

        Assert.NotNull(source);
        Assert.True(source.State.Track.SafetyCar);
        Assert.Equal("BOX_L_MEDIUM", source.Snapshot.Outcomes[1].StrategyId);
        Assert.Equal([SafetyCar], source.Injected);
    }

    [Fact]
    public void Select_RealScenario_UsesRealStrategy()
    {
        var real = Real();

        var source = JevStrategySource.Select(real, Hypothetical(), hypotheticalScenario: false, scenarioHasActiveEvents: false);

        Assert.Same(real, source);
    }

    [Fact]
    public void Select_ScenarioWithoutActiveEvents_UsesRealStrategy()
    {
        var real = Real();

        var source = JevStrategySource.Select(real, null, hypotheticalScenario: true, scenarioHasActiveEvents: false);

        Assert.Same(real, source);
    }

    [Fact]
    public void Select_ScenarioStrategyNotCalculatedYet_ReturnsNull()
    {
        var source = JevStrategySource.Select(Real(), null, hypotheticalScenario: true, scenarioHasActiveEvents: true);

        Assert.Null(source);
    }

    [Fact]
    public void Strategy_HypotheticalSource_StateHasScenarioStateMonteCarloAndInjected()
    {
        var request = Request(Hypothetical(), hypothetical: true);

        Assert.Contains("\"track\":{\"flag\":\"YELLOW\",\"sc\":true,\"vsc\":false}", request.State, StringComparison.Ordinal);
        Assert.Contains($"\"injected\":[\"{SafetyCar}\"]", request.State, StringComparison.Ordinal);
        Assert.Contains("{\"id\":\"BOX_L_MEDIUM\",\"exp_pos\":9.4,\"p_gain\":0.72}", request.State, StringComparison.Ordinal);
        Assert.DoesNotContain("BOX_L_INTERMEDIATE", request.State, StringComparison.Ordinal);
    }

    [Fact]
    public void Strategy_RealSource_RequestUnchanged()
    {
        var expected = JevRequestFactory.Strategy("jev-latest", JevTestRace.StrategyInputLap60());

        var request = Request(Real(), hypothetical: false);

        Assert.Equal(JevSerializer.SerializeRequest(expected), JevSerializer.SerializeRequest(request));
    }

    [Fact]
    public void Strategy_SameSourceRealAndHypothetical_DifferentCacheKey()
    {
        var real = Request(Real(), hypothetical: false);
        var hypothetical = Request(Real(), hypothetical: true);

        Assert.Equal(real.State, hypothetical.State);
        Assert.NotEqual(JevSerializer.ComputeStateHash(real), JevSerializer.ComputeStateHash(hypothetical));
    }

    [Fact]
    public async Task ConsultAsync_HypotheticalScenarioAfterReal_ScenarioDecisionHypotheticalNotFromCache()
    {
        await ConsultAsync(Real(), null);

        var decision = await ConsultAsync(Hypothetical(), ScenarioId);

        Assert.Equal(2, _inner.Calls);
        Assert.True(decision.Hypothetical);
        Assert.False(decision.FromCache);
        Assert.Equal(ScenarioId, decision.ScenarioId);
        Assert.Equal(PitCall.Box, decision.PitCall);
        Assert.Equal(Compound.Medium, decision.Compound);
        Assert.Contains(Reason.ScDeployed, decision.Reasons);
    }

    [Fact]
    public async Task ConsultAsync_ScenarioWithoutActiveEventsAfterReal_DoesNotReuseRealAnswer()
    {
        var real = Real();
        await ConsultAsync(real, null);

        var decision = await ConsultAsync(JevStrategySource.Select(real, null, true, false)!, ScenarioId);

        Assert.Equal(2, _inner.Calls);
        Assert.True(decision.Hypothetical);
        Assert.False(decision.FromCache);
    }

    [Fact]
    public async Task ConsultAsync_SameHypotheticalTwice_SecondFromCacheAndHypothetical()
    {
        await ConsultAsync(Hypothetical(), ScenarioId);

        var decision = await ConsultAsync(Hypothetical(), ScenarioId);

        Assert.Equal(1, _inner.Calls);
        Assert.True(decision.Hypothetical);
        Assert.True(decision.FromCache);
    }

    [Fact]
    public async Task ConsultAsync_RealScenarioTwice_RealDecisionSecondFromCache()
    {
        var first = await ConsultAsync(Real(), null);

        var second = await ConsultAsync(Real(), null);

        Assert.Equal(1, _inner.Calls);
        Assert.False(first.Hypothetical);
        Assert.Null(first.ScenarioId);
        Assert.False(first.FromCache);
        Assert.False(second.Hypothetical);
        Assert.True(second.FromCache);
        Assert.Equal(PitCall.Box, second.PitCall);
        Assert.Equal(Compound.Intermediate, second.Compound);
    }

    private static JevStrategySource Real() => new(JevTestRace.StateLap60(), JevTestRace.SnapshotLap60(), []);

    // Safety car inyectado en la vuelta 60: el estado y el Monte Carlo del escenario cambian; el replay no.
    private static JevStrategySource Hypothetical()
    {
        var state = JevTestRace.StateLap60() with { Track = new TrackStatus("YELLOW", SafetyCar: true, VirtualSafetyCar: false) };
        var snapshot = JevTestRace.SnapshotLap60() with
        {
            Outcomes =
            [
                JevTestRace.Outcome("STAY_OUT", PitCall.StayOut, null, null, 13.5, 0.1),
                JevTestRace.Outcome("BOX_L_MEDIUM", PitCall.Box, 60, Compound.Medium, 9.4, 0.72),
                JevTestRace.Outcome("BOX_L+1_MEDIUM", PitCall.BoxNextLap, 61, Compound.Medium, 11.0, 0.55),
            ],
            Reasons = [Reason.ScDeployed, Reason.TyreCliffNear],
        };
        return new JevStrategySource(state, snapshot, [SafetyCar]);
    }

    private static JevRequest Request(JevStrategySource source, bool hypothetical) =>
        JevRequestFactory.Strategy(
            "jev-latest",
            new JevStrategyInput(
                JevTestRequests.SessionKey,
                source.State,
                source.Snapshot,
                JevTestRace.LapsTotal,
                ["LOW GRIP CONDITIONS", "OVERTAKE DISABLED"],
                source.Injected,
                TopOutcomes: 5,
                hypothetical));

    private async Task<JevDecision> ConsultAsync(JevStrategySource source, Guid? scenarioId)
    {
        var request = Request(source, scenarioId is not null);
        var response = await _client.SystemOneAsync(request, CancellationToken.None);
        var input = new JevDecisionInput(
            Guid.CreateVersion7(_time.GetUtcNow()),
            JevTestRequests.SessionKey,
            scenarioId,
            source.Snapshot.DriverNumber,
            source.Snapshot.Lap,
            JevTrigger.Manual,
            source.Snapshot.Reasons);
        var decision = JevDecisionMapper.Map(input, request, response);
        Assert.NotNull(decision);
        return decision;
    }
}
