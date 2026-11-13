using System.Diagnostics.Metrics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using PitWall.Jev;
using PitWall.Jev.Guards;

namespace PitWall.Tests.Jev;

public sealed class JevServiceCollectionExtensionsTests
{
    [Fact]
    public async Task AddPitWallJev_ClientFake_ResolvesGuardedFakeClient()
    {
        using var provider = BuildProvider("Fake");

        var client = provider.GetRequiredService<IJevClient>();
        var response = await client.SystemOneAsync(JevTestRequests.StrategyLap60(), CancellationToken.None);

        Assert.IsType<GuardedJevClient>(client);
        Assert.Equal(JevResponseStatus.Answered, response.Status);
        Assert.Equal("BOX", response.Answers["pit_call"].Choice);
    }

    [Fact]
    public async Task AddPitWallJev_ClientRealWithoutRealClient_FailsWithAlert()
    {
        using var provider = BuildProvider("Real");

        var response = await provider.GetRequiredService<IJevClient>()
            .SystemOneAsync(JevTestRequests.StrategyLap60(), CancellationToken.None);

        Assert.Equal(JevResponseStatus.Failed, response.Status);
        Assert.Equal(JevAlertKind.ClientFailed, Assert.Single(provider.GetRequiredService<JevAlerts>().Active).Kind);
    }

    [Fact]
    public void AddPitWallJev_NegativeMaxRequests_FailsValidation()
    {
        using var provider = BuildProvider("Fake", maxRequests: "-1");

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<JevOptions>>().Value);
    }

    private static ServiceProvider BuildProvider(string client, string maxRequests = "500")
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jev:Client"] = client,
                ["Jev:MaxRequestsPerSession"] = maxRequests,
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
