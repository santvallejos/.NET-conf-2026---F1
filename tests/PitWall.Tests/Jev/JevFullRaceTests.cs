using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using PitWall.Domain;
using PitWall.Jev;
using PitWall.Jev.Caching;
using PitWall.Jev.Decisions;
using PitWall.Jev.Fake;
using PitWall.Jev.Guards;
using PitWall.Jev.Requests;
using PitWall.Jev.Triage;
using PitWall.Jev.Triggers;

namespace PitWall.Tests.Jev;

/// <summary>
/// Carrera completa (71 vueltas, 4 pilotos, modo EveryLap) con <see cref="FakeJevClient"/> detrás de las guardas:
/// triage ante Race Control, estrategia por vuelta, mapeo a decisiones. Mismo flujo que el worker del host.
/// </summary>
public sealed class JevFullRaceTests : IDisposable
{
    private static readonly int[] Drivers = [43, 44, 12, 3];

    private static readonly Dictionary<int, RaceControlMessage[]> MessagesByLap = new()
    {
        [1] = [Message(1, "Flag", "GREEN", "Track", null, "GREEN LIGHT - PIT EXIT OPEN")],
        [15] = [Message(15, "Flag", "BLUE", "Driver", 44, "WAVED BLUE FLAG FOR CAR 44 (HAM)")],
        [16] = [Message(16, "Flag", "CLEAR", "Sector", null, "CLEAR IN TRACK SECTOR 3")],
        [40] = [Message(40, "SafetyCar", null, "Track", null, "VIRTUAL SAFETY CAR DEPLOYED")],
        [41] = [Message(41, "SafetyCar", null, "Track", null, "VIRTUAL SAFETY CAR ENDING")],
        [58] = [Message(58, "Other", null, null, null, "RISK OF RAIN FOR F1 RACE IS 60%")],
        [60] = [Message(60, "Other", null, null, null, "LOW GRIP CONDITIONS"), Message(60, "Other", null, "Driver", 43, "CAR 43 (COL) TIME DELETED - TRACK LIMITS")],
    };

    private readonly FakeTimeProvider _time = new(JevTestRace.RaceStart);
    private readonly TestMeterFactory _meterFactory = new();
    private readonly StaticOptionsMonitor<JevOptions> _options = new(new JevOptions { Drivers = Drivers, Mode = JevMode.EveryLap });
    private readonly GuardedJevClient _client;

    public JevFullRaceTests()
    {
        _client = new GuardedJevClient(
            new FakeJevClient(),
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
    public async Task FullRace_FakeClientEveryLap_DecisionForEveryDriverAndLapWithoutErrors()
    {
        var decisions = await RunRaceAsync();

        var strategy = decisions.Where(d => d.Kind == JevKind.Strategy).ToList();
        Assert.Equal(JevTestRace.LapsTotal * Drivers.Length, strategy.Count);
        Assert.All(strategy, d =>
        {
            Assert.Equal(JevTrigger.EveryLap, d.Trigger);
            Assert.NotNull(d.PitCall);
            Assert.NotNull(d.Compound);
            Assert.NotNull(d.PaceMode);
            Assert.NotNull(d.EnergyMode);
            Assert.InRange(d.Confidence, 0.0001, 1.0);
            Assert.Equal(d.Probabilities.Values.Min(p => p.Values.Max()), d.Confidence, 4);
            Assert.False(d.FromCache);
            Assert.Equal(0m, d.CostUsd);
        });

        // Triage: verde (4 pilotos), VSC x2 (4 cada uno), riesgo de lluvia (4), lap 60 (4); azules y CLEAR no.
        var triage = decisions.Where(d => d.Kind == JevKind.Triage).ToList();
        Assert.Equal(20, triage.Count);
        Assert.DoesNotContain(triage, d => d.Lap is 15 or 16);
        Assert.All(triage, d => Assert.Equal(1, d.Urgency));
        Assert.True(_client.RequestCount(JevTestRequests.SessionKey) <= _options.CurrentValue.MaxRequestsPerSession);
    }

    [Fact]
    public async Task FullRace_SecondPass_AllFromCacheAtZeroCost()
    {
        var first = await RunRaceAsync();
        var requests = _client.RequestCount(JevTestRequests.SessionKey);

        var second = await RunRaceAsync();

        Assert.Equal(first.Count, second.Count);
        Assert.All(second, d => Assert.True(d.FromCache));
        Assert.Equal(requests, _client.RequestCount(JevTestRequests.SessionKey));
    }

    [Fact]
    public async Task FullRace_SpecificLaps_OnlyConfiguredLaps()
    {
        _options.CurrentValue.Mode = JevMode.SpecificLaps;
        _options.CurrentValue.Laps = [18, 40, 60, 62];

        var decisions = await RunRaceAsync();

        Assert.Equal(
            [18, 40, 60, 62],
            decisions.Where(d => d.Kind == JevKind.Strategy).Select(d => d.Lap).Distinct().Order());
    }

    [Fact]
    public async Task FullRace_DriverSwitchedOffMidRace_NoMoreDecisionsForThatDriver()
    {
        var decisions = await RunRaceAsync(lap =>
        {
            if (lap == 30)
            {
                _options.CurrentValue.Drivers = [43, 12, 3];
            }
        });

        Assert.Equal(29, decisions.Count(d => d.Kind == JevKind.Strategy && d.DriverNumber == 44));
        Assert.Equal(JevTestRace.LapsTotal, decisions.Count(d => d.Kind == JevKind.Strategy && d.DriverNumber == 43));
    }

    private async Task<List<JevDecision>> RunRaceAsync(Action<int>? beforeLap = null)
    {
        var decisions = new List<JevDecision>();
        for (var lap = 1; lap <= JevTestRace.LapsTotal; lap++)
        {
            beforeLap?.Invoke(lap);
            var messages = MessagesByLap.GetValueOrDefault(lap, []);
            var state = State(lap, messages);

            foreach (var driver in _options.CurrentValue.Drivers)
            {
                var relevant = messages.Where(m => RaceControlRelevance.IsRelevant(m, driver)).Select(m => m.Message).ToList();
                if (relevant.Count > 0 && JevTriggerPolicy.ShouldTriage(_options.CurrentValue, driver))
                {
                    var triage = JevRequestFactory.Triage("jev-latest", new JevTriageInput(JevTestRequests.SessionKey, state, driver, relevant, []));
                    decisions.Add(await DecideAsync(triage, driver, lap, JevTrigger.Triage));
                }

                if (JevTriggerPolicy.OnLapCompleted(_options.CurrentValue, driver, lap) is { } trigger)
                {
                    var input = new JevStrategyInput(JevTestRequests.SessionKey, state, Snapshot(driver, lap), JevTestRace.LapsTotal, relevant, [], 5);
                    decisions.Add(await DecideAsync(JevRequestFactory.Strategy("jev-latest", input), driver, lap, trigger));
                }
            }
        }

        return decisions;
    }

    private async Task<JevDecision> DecideAsync(JevRequest request, int driver, int lap, JevTrigger trigger)
    {
        var response = await _client.SystemOneAsync(request, CancellationToken.None);
        var input = new JevDecisionInput(Guid.CreateVersion7(_time.GetUtcNow()), JevTestRequests.SessionKey, null, driver, lap, trigger, [Reason.TyreDegHigh]);
        return JevDecisionMapper.Map(input, request, response) ?? throw new InvalidOperationException($"Jev did not answer: {response.Status}");
    }

    private static RaceState State(int lap, RaceControlMessage[] messages) => new(
        JevTestRequests.SessionKey,
        JevTestRace.RaceStart.AddSeconds(90 * lap),
        lap,
        [.. Drivers.Select((driver, index) => new CarState(driver, index + 8, lap, 10.0 + index, index == 0 ? null : 1.0, Compound.Medium, lap % 30, InPit: false))],
        new WeatherState(22, 35, 50, Rainfall: lap >= 60),
        new TrackStatus("GREEN", SafetyCar: false, VirtualSafetyCar: lap == 40),
        messages);

    // Gomas que se degradan con la edad: al principio conviene seguir, cerca del cliff parar.
    private static StrategySnapshot Snapshot(int driver, int lap)
    {
        var age = lap % 30;
        var stayOut = 9.0 + (age / 10.0);
        return new StrategySnapshot(
            driver,
            lap,
            new TyreModel(Compound.Medium, age, 80.0, 0.08, 0.2, Math.Max(0, 25 - age), false),
            new EnergyEstimate(60, 1, 1, false, true),
            [
                JevTestRace.Outcome("STAY_OUT", PitCall.StayOut, null, null, stayOut, 0.3),
                JevTestRace.Outcome("BOX_L_HARD", PitCall.Box, lap, Compound.Hard, 11.0, 0.2),
                JevTestRace.Outcome("BOX_L+1_HARD", PitCall.BoxNextLap, lap + 1, Compound.Hard, 11.2, 0.2),
            ],
            [Reason.TyreDegHigh],
            [new Constraint("DRY_TRACK", [], [Compound.Wet], null, false)]);
    }

    private static RaceControlMessage Message(int lap, string category, string? flag, string? scope, int? driver, string text) =>
        new(JevTestRace.RaceStart.AddSeconds(90 * lap), lap, category, flag, scope, driver, text);
}
