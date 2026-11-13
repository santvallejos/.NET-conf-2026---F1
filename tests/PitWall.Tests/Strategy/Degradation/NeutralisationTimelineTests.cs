using PitWall.Domain;
using PitWall.Strategy.Degradation;

namespace PitWall.Tests.Strategy.Degradation;

public sealed class NeutralisationTimelineTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 8, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IsNeutralised_LapDuringSafetyCar_ReturnsTrue()
    {
        var timeline = new NeutralisationTimeline(
        [
            Message(100, "SafetyCar", null, null, "SAFETY CAR DEPLOYED"),
            Message(400, "Flag", "GREEN", "Track", "TRACK CLEAR"),
        ]);

        Assert.True(timeline.IsNeutralised(At(150), At(240)));
        Assert.True(timeline.IsNeutralised(At(50), At(120)));
        Assert.False(timeline.IsNeutralised(At(0), At(90)));
        Assert.False(timeline.IsNeutralised(At(410), At(500)));
    }

    [Fact]
    public void IsNeutralised_VirtualSafetyCarUntilEnding_ReturnsTrueOnlyInside()
    {
        var timeline = new NeutralisationTimeline(
        [
            Message(100, "SafetyCar", null, null, "VIRTUAL SAFETY CAR DEPLOYED"),
            Message(200, "SafetyCar", null, null, "VIRTUAL SAFETY CAR ENDING"),
        ]);

        Assert.True(timeline.IsNeutralised(At(150), At(160)));
        Assert.False(timeline.IsNeutralised(At(210), At(300)));
    }

    [Fact]
    public void IsNeutralised_SectorYellowUntilClear_ReturnsTrueOnlyInside()
    {
        var timeline = new NeutralisationTimeline(
        [
            Message(100, "Flag", "YELLOW", "Sector", "YELLOW IN TRACK SECTOR 5"),
            Message(130, "Flag", "YELLOW", "Sector", "YELLOW IN TRACK SECTOR 6"),
            Message(150, "Flag", "CLEAR", "Sector", "CLEAR IN TRACK SECTOR 5"),
            Message(170, "Flag", "CLEAR", "Sector", "CLEAR IN TRACK SECTOR 6"),
        ]);

        Assert.True(timeline.IsNeutralised(At(160), At(165)));
        Assert.False(timeline.IsNeutralised(At(171), At(260)));
    }

    [Fact]
    public void IsNeutralised_RedFlagNeverCleared_StaysNeutralised()
    {
        var timeline = new NeutralisationTimeline([Message(100, "Flag", "RED", "Track", "RED FLAG")]);

        Assert.True(timeline.IsNeutralised(At(5000), At(5090)));
    }

    [Fact]
    public void IsNeutralised_NoMessages_ReturnsFalse()
    {
        Assert.False(new NeutralisationTimeline([]).IsNeutralised(At(0), At(100)));
    }

    private static DateTimeOffset At(int seconds) => Start.AddSeconds(seconds);

    private static RaceControlMessage Message(int seconds, string category, string? flag, string? scope, string text) =>
        new(At(seconds), LapNumber: null, category, flag, scope, DriverNumber: null, text);
}
