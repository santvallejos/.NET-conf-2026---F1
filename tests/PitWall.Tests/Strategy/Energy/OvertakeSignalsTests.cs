using PitWall.Domain;
using PitWall.Strategy.Energy;

namespace PitWall.Tests.Strategy.Energy;

public sealed class OvertakeSignalsTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 8, 16, 13, 0, TimeSpan.Zero);

    [Fact]
    public void IsOvertakeDisabled_NoMessages_ReturnsFalse() =>
        Assert.False(OvertakeSignals.IsOvertakeDisabled([]));

    [Fact]
    public void IsOvertakeDisabled_OvertakeDisabledMessage_ReturnsTrue() =>
        Assert.True(OvertakeSignals.IsOvertakeDisabled([Message(0, "LOW GRIP CONDITIONS"), Message(2, "OVERTAKE DISABLED")]));

    [Fact]
    public void IsOvertakeDisabled_EnabledAfterDisabled_ReturnsFalse() =>
        Assert.False(OvertakeSignals.IsOvertakeDisabled([Message(0, "OVERTAKE DISABLED"), Message(600, "OVERTAKE ENABLED")]));

    [Fact]
    public void IsOvertakeDisabled_UnorderedMessages_UsesTheLatestByDate() =>
        Assert.True(OvertakeSignals.IsOvertakeDisabled([Message(3000, "OVERTAKE DISABLED"), Message(600, "OVERTAKE ENABLED"), Message(0, "OVERTAKE DISABLED")]));

    [Fact]
    public void IsOvertakeDisabled_MixedCase_IsRecognised() =>
        Assert.True(OvertakeSignals.IsOvertakeDisabled([Message(0, "Overtake Disabled")]));

    [Fact]
    public void IsOvertakeDisabled_UnrelatedMessages_ReturnsFalse() =>
        Assert.False(OvertakeSignals.IsOvertakeDisabled([Message(0, "DRS DISABLED"), Message(5, "CLEAR IN TRACK SECTOR 16")]));

    private static RaceControlMessage Message(int seconds, string text) =>
        new(Start.AddSeconds(seconds), 1, "Other", null, null, null, text);
}
