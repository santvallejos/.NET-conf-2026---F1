using PitWall.Domain;
using PitWall.Jev;
using PitWall.Jev.Triggers;

namespace PitWall.Tests.Jev;

public sealed class JevTriggerPolicyTests
{
    [Fact]
    public void OnLapCompleted_EveryLap_TriggersEveryLap()
    {
        var options = Options(JevMode.EveryLap);

        Assert.All(Enumerable.Range(1, 71), lap => Assert.Equal(JevTrigger.EveryLap, JevTriggerPolicy.OnLapCompleted(options, 43, lap)));
    }

    [Theory]
    [InlineData(18, true)]
    [InlineData(19, false)]
    [InlineData(60, true)]
    [InlineData(62, true)]
    public void OnLapCompleted_SpecificLaps_OnlyConfiguredLaps(int lap, bool triggers)
    {
        var options = Options(JevMode.SpecificLaps);

        Assert.Equal(triggers ? JevTrigger.SpecificLap : null, JevTriggerPolicy.OnLapCompleted(options, 43, lap));
    }

    [Fact]
    public void OnLapCompleted_Manual_NeverTriggers()
    {
        var options = Options(JevMode.Manual);

        Assert.All(Enumerable.Range(1, 71), lap => Assert.Null(JevTriggerPolicy.OnLapCompleted(options, 43, lap)));
    }

    [Fact]
    public void OnLapCompleted_GloballyDisabled_NeverTriggers()
    {
        var options = Options(JevMode.EveryLap);
        options.Enabled = false;

        Assert.Null(JevTriggerPolicy.OnLapCompleted(options, 43, 10));
        Assert.False(JevTriggerPolicy.ShouldTriage(options, 43));
    }

    [Fact]
    public void OnLapCompleted_DriverNotInDrivers_NeverTriggers()
    {
        var options = Options(JevMode.EveryLap);

        Assert.Null(JevTriggerPolicy.OnLapCompleted(options, 1, 10));
        Assert.False(JevTriggerPolicy.ShouldTriage(options, 1));
    }

    [Theory]
    [InlineData(JevMode.EveryLap)]
    [InlineData(JevMode.SpecificLaps)]
    [InlineData(JevMode.Manual)]
    public void ShouldTriage_AnyMode_WhenTriageEnabled(JevMode mode)
    {
        var options = Options(mode);

        Assert.True(JevTriggerPolicy.ShouldTriage(options, 43));

        options.Triage.Enabled = false;
        Assert.False(JevTriggerPolicy.ShouldTriage(options, 43));
    }

    [Theory]
    [InlineData(Impact.ActNow, true, true)]
    [InlineData(Impact.ActNow, false, false)]
    [InlineData(Impact.Monitor, true, false)]
    [InlineData(Impact.None, true, false)]
    public void TriageTriggersStrategy_OnlyActNowWhenAllowed(Impact impact, bool canTrigger, bool expected)
    {
        var options = Options(JevMode.Manual);
        options.Triage.CanTriggerStrategy = canTrigger;

        Assert.Equal(expected, JevTriggerPolicy.TriageTriggersStrategy(options, Triage(impact)));
    }

    private static JevOptions Options(JevMode mode) => new()
    {
        Mode = mode,
        Laps = [18, 40, 60, 62],
        Drivers = [43, 44, 12, 3],
    };

    private static JevDecision Triage(Impact impact) => new(
        Guid.NewGuid(), 11412, null, 43, 40, JevKind.Triage, JevTrigger.Triage,
        null, null, null, null, impact, 4, 0.8,
        new Dictionary<string, IReadOnlyDictionary<string, double>>(), [], false, false, 0, 0, 0m);
}
