using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using PitWall.Replay;

namespace PitWall.Tests.Replay;

public sealed class ReplayClockTests : IDisposable
{
    private readonly ReplayTestDatabase _database = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly ReplayClock _clock;

    public ReplayClockTests()
    {
        _clock = new ReplayClock(_timeProvider, Options.Create(new ReplayOptions()));
        using var context = _database.CreateDbContext();
        _clock.Load(ReplaySession.LoadAsync(context, ReplayTestDatabase.SessionKey).GetAwaiter().GetResult());
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public void Load_NewSession_StartsPausedAtRaceStartWithSlowestSpeed()
    {
        _timeProvider.Advance(TimeSpan.FromMinutes(1));

        Assert.False(_clock.IsPlaying);
        Assert.Equal(1, _clock.Speed);
        Assert.Equal(ReplayTestDatabase.RaceStart, _clock.Now);
    }

    [Theory]
    [InlineData(1, 30)]
    [InlineData(5, 150)]
    [InlineData(20, 600)]
    public void Play_AtSpeed_AdvancesVirtualTimeBySpeedTimesElapsed(int speed, double expectedSeconds)
    {
        _clock.SetSpeed(speed);
        _clock.Play();

        _timeProvider.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(ReplayTestDatabase.RaceStart.AddSeconds(Math.Min(expectedSeconds, 382)), _clock.Now);
    }

    [Fact]
    public void Pause_AfterPlaying_FreezesVirtualTime()
    {
        _clock.Play();
        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        _clock.Pause();
        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        Assert.False(_clock.IsPlaying);
        Assert.Equal(ReplayTestDatabase.At(10), _clock.Now);
    }

    [Fact]
    public void SetSpeed_WhilePlaying_KeepsElapsedVirtualTime()
    {
        _clock.Play();
        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        _clock.SetSpeed(5);
        _timeProvider.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(ReplayTestDatabase.At(20), _clock.Now);
    }

    [Fact]
    public void SetSpeed_NotConfigured_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _clock.SetSpeed(3));
    }

    [Fact]
    public void JumpToLap_WhilePlaying_RepositionsAndKeepsPlaying()
    {
        _clock.Play();
        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        var target = _clock.JumpToLap(4);
        _timeProvider.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(ReplayTestDatabase.At(270), target);
        Assert.True(_clock.IsPlaying);
        Assert.Equal(ReplayTestDatabase.At(271), _clock.Now);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void JumpToLap_OutsideSession_ThrowsArgumentOutOfRange(int lap)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _clock.JumpToLap(lap));
    }

    [Fact]
    public void Play_WithoutSession_ThrowsInvalidOperation()
    {
        var clock = new ReplayClock(_timeProvider, Options.Create(new ReplayOptions()));

        Assert.Throws<InvalidOperationException>(clock.Play);
    }
}
