using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using PitWall.Domain;
using PitWall.Replay;

namespace PitWall.Tests.Replay;

public sealed class ReplayEngineTests : IDisposable
{
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(10);

    private readonly ReplayTestDatabase _database = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly ReplayEngine _engine;

    public ReplayEngineTests()
    {
        var options = Options.Create(new ReplayOptions());
        _engine = new ReplayEngine(new ReplayClock(_timeProvider, options), _database, _timeProvider, options, NullLogger<ReplayEngine>.Instance);
    }

    public void Dispose()
    {
        _engine.Dispose();
        _database.Dispose();
    }

    [Fact]
    public async Task JumpToLap_WithCachedSession_BuildsExpectedRaceState()
    {
        await _engine.LoadSessionAsync(ReplayTestDatabase.SessionKey);
        _database.Dispose();

        var state = _engine.JumpToLap(3);

        var expectedCars = new[]
        {
            new CarState(12, 1, 3, 0, 0, Compound.Medium, 2, InPit: false),
            new CarState(43, 2, 2, 1.2, 1.2, Compound.Soft, 1, InPit: true),
            new CarState(44, 3, 2, 2.0, 0.8, Compound.Hard, 3, InPit: false),
        };
        Assert.Equal(ReplayTestDatabase.SessionKey, state.SessionKey);
        Assert.Equal(ReplayTestDatabase.At(180), state.Clock);
        Assert.Equal(3, state.LeaderLap);
        Assert.Equal(expectedCars, state.Cars);
        Assert.Equal(new WeatherState(24.5, 39.0, 55, Rainfall: true), state.Weather);
        Assert.Equal(new TrackStatus("GREEN", SafetyCar: false, VirtualSafetyCar: true), state.Track);
        Assert.Empty(state.NewMessages);
        Assert.Equal(ReplayTestDatabase.At(180), _engine.Clock.Now);
    }

    [Fact]
    public async Task Tick_AfterPlayingFromJump_EmitsStateWithNewRaceControlMessages()
    {
        await _engine.LoadSessionAsync(ReplayTestDatabase.SessionKey);
        _engine.JumpToLap(3);
        using var subscription = _engine.Subscribe();
        Assert.True(subscription.Reader.TryRead(out var current));
        Assert.Equal(ReplayTestDatabase.At(180), current.Clock);

        _engine.SetSpeed(5);
        _engine.Play();
        _timeProvider.Advance(TimeSpan.FromSeconds(4));
        _engine.Tick();

        Assert.True(subscription.Reader.TryRead(out var state));
        Assert.Equal(ReplayTestDatabase.At(200), state.Clock);
        var message = Assert.Single(state.NewMessages);
        Assert.Equal("VIRTUAL SAFETY CAR ENDING", message.Message);
        Assert.Equal(new TrackStatus("GREEN", SafetyCar: false, VirtualSafetyCar: false), state.Track);
        var car43 = Assert.Single(state.Cars, car => car.DriverNumber == 43);
        Assert.Equal(new CarState(43, 3, 3, 21.5, 19.5, Compound.Hard, 0, InPit: false), car43);
        Assert.Equal([12, 44, 43], state.Cars.Select(car => car.DriverNumber));
    }

    [Fact]
    public async Task Tick_WhilePaused_DoesNotEmit()
    {
        await _engine.LoadSessionAsync(ReplayTestDatabase.SessionKey);
        using var subscription = _engine.Subscribe();
        Assert.True(subscription.Reader.TryRead(out _));

        _timeProvider.Advance(TimeSpan.FromSeconds(30));

        Assert.Null(_engine.Tick());
        Assert.False(subscription.Reader.TryRead(out _));
    }

    [Fact]
    public async Task Tick_PastSessionEnd_ClampsClockAndPauses()
    {
        await _engine.LoadSessionAsync(ReplayTestDatabase.SessionKey);
        _engine.SetSpeed(20);
        _engine.Play();

        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        var state = _engine.Tick();

        Assert.NotNull(state);
        Assert.Equal(ReplayTestDatabase.At(382), state.Clock);
        Assert.Equal(4, state.LeaderLap);
        Assert.False(_engine.Clock.IsPlaying);
    }

    [Fact]
    public async Task ExecuteAsync_WhilePlaying_EmitsRaceStatePerTick()
    {
        await _engine.LoadSessionAsync(ReplayTestDatabase.SessionKey);
        using var subscription = _engine.Subscribe();
        Assert.True(subscription.Reader.TryRead(out _));
        await _engine.StartAsync(CancellationToken.None);
        _engine.Play();

        _timeProvider.Advance(TimeSpan.FromMilliseconds(250));
        var first = await ReadAsync(subscription);
        _timeProvider.Advance(TimeSpan.FromMilliseconds(250));
        var second = await ReadAsync(subscription);

        Assert.Equal(ReplayTestDatabase.At(0.25), first.Clock);
        Assert.Equal(ReplayTestDatabase.At(0.5), second.Clock);
        await _engine.StopAsync(CancellationToken.None);
        Assert.True(subscription.Reader.Completion.IsCompleted);
    }

    [Fact]
    public async Task Dispose_Subscription_StopsReceivingAndCompletesReader()
    {
        await _engine.LoadSessionAsync(ReplayTestDatabase.SessionKey);
        var subscription = _engine.Subscribe();

        subscription.Dispose();
        _engine.JumpToLap(2);

        Assert.True(subscription.Reader.TryRead(out var initial));
        Assert.Equal(ReplayTestDatabase.RaceStart, initial.Clock);
        Assert.False(subscription.Reader.TryRead(out _));
        Assert.True(subscription.Reader.Completion.IsCompleted);
    }

    [Fact]
    public async Task LoadSessionAsync_WithoutLaps_ThrowsReplaySessionException()
    {
        using var empty = new ReplayTestDatabase(seed: false);
        var options = Options.Create(new ReplayOptions());
        using var engine = new ReplayEngine(new ReplayClock(_timeProvider, options), empty, _timeProvider, options, NullLogger<ReplayEngine>.Instance);

        await Assert.ThrowsAsync<ReplaySessionException>(() => engine.LoadSessionAsync(ReplayTestDatabase.SessionKey));
    }

    private static async Task<RaceState> ReadAsync(ReplaySubscription subscription)
    {
        using var timeout = new CancellationTokenSource(ReadTimeout);
        return await subscription.Reader.ReadAsync(timeout.Token);
    }
}
