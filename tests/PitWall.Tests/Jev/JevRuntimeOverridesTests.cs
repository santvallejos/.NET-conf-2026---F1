using System.Diagnostics.Metrics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using PitWall.Jev;
using PitWall.Jev.Runtime;

namespace PitWall.Tests.Jev;

public sealed class JevRuntimeOverridesTests : IDisposable
{
    private readonly ServiceProvider _provider = BuildProvider();

    private IOptionsMonitor<JevOptions> Monitor => _provider.GetRequiredService<IOptionsMonitor<JevOptions>>();

    private JevRuntimeOverrides Overrides => _provider.GetRequiredService<JevRuntimeOverrides>();

    public void Dispose() => _provider.Dispose();

    [Fact]
    public void CurrentValue_NoOverrides_ReadsConfiguration()
    {
        var options = Monitor.CurrentValue;

        Assert.True(options.Enabled);
        Assert.Equal(JevMode.SpecificLaps, options.Mode);
        Assert.Equal([18, 40, 60, 62], options.Laps);
        Assert.Equal([43, 44, 12, 3], options.Drivers);
        Assert.True(options.Triage.Enabled);
        Assert.False(options.Triage.CanTriggerStrategy);
    }

    [Fact]
    public async Task SetEnabled_False_MonitorAndGuardedClientSeeItWithoutRestart()
    {
        var client = _provider.GetRequiredService<IJevClient>();
        Assert.Equal(JevResponseStatus.Answered, (await client.SystemOneAsync(JevTestRequests.StrategyForLap(1), CancellationToken.None)).Status);

        Overrides.SetEnabled(false);

        Assert.False(Monitor.CurrentValue.Enabled);
        Assert.Equal(JevResponseStatus.Disabled, (await client.SystemOneAsync(JevTestRequests.StrategyForLap(2), CancellationToken.None)).Status);

        Overrides.SetEnabled(true);

        Assert.Equal(JevResponseStatus.Answered, (await client.SystemOneAsync(JevTestRequests.StrategyForLap(3), CancellationToken.None)).Status);
    }

    [Fact]
    public void SetDriverEnabled_Off_RemovesOnlyThatDriver()
    {
        Overrides.SetDriverEnabled(44, enabled: false);

        Assert.Equal([43, 12, 3], Monitor.CurrentValue.Drivers);

        Overrides.SetDriverEnabled(44, enabled: true);
        Overrides.SetDriverEnabled(1, enabled: true);

        Assert.Equal([43, 12, 3, 44, 1], Monitor.CurrentValue.Drivers);
    }

    [Fact]
    public void SetDrivers_ReplacesListAndClearsToggles()
    {
        Overrides.SetDriverEnabled(43, enabled: false);

        Overrides.SetDrivers([43, 16]);

        Assert.Equal([43, 16], Monitor.CurrentValue.Drivers);
    }

    [Fact]
    public void SetModeAndLaps_AppliedOverConfiguration()
    {
        Overrides.SetMode(JevMode.EveryLap);
        Overrides.SetLaps([30, 10, 30]);

        var options = Monitor.CurrentValue;
        Assert.Equal(JevMode.EveryLap, options.Mode);
        Assert.Equal([10, 30], options.Laps);
        Assert.Equal("jev-latest", options.Model);
    }

    [Fact]
    public void SetLaps_LapBelowOne_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Overrides.SetLaps([0]));
    }

    [Fact]
    public void AnyChange_NotifiesOnChangeListeners()
    {
        var changes = new List<JevOptions>();
        using var registration = Monitor.OnChange(options => changes.Add(options));

        Overrides.SetEnabled(false);
        Overrides.SetDriverEnabled(3, enabled: false);

        Assert.Equal(2, changes.Count);
        Assert.False(changes[^1].Enabled);
        Assert.DoesNotContain(3, changes[^1].Drivers);
    }

    private static ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jev:Mode"] = "SpecificLaps",
                ["Jev:Laps:0"] = "18",
                ["Jev:Laps:1"] = "40",
                ["Jev:Laps:2"] = "60",
                ["Jev:Laps:3"] = "62",
                ["Jev:Drivers:0"] = "43",
                ["Jev:Drivers:1"] = "44",
                ["Jev:Drivers:2"] = "12",
                ["Jev:Drivers:3"] = "3",
                ["Jev:Triage:CanTriggerStrategy"] = "false",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IMeterFactory, TestMeterFactory>();
        services.AddPitWallJev();

        return services.BuildServiceProvider();
    }
}
