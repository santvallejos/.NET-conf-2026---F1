using System.Diagnostics.Metrics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using PitWall.Jev;
using PitWall.Jev.Guards;
using PitWall.Jev.Real;
using PitWall.Jev.Runtime;

namespace PitWall.Tests.Jev.Real;

public sealed class JevRealServiceCollectionExtensionsTests
{
    private const int AlertSessionKey = 11412;

    [Fact]
    public async Task AddPitWallJevReal_ClientRealWithoutKey_StartsDisabledWithAlert()
    {
        using var provider = BuildProvider("Real", apiKey: null);

        await StartHostedServicesAsync(provider);

        Assert.False(provider.GetRequiredService<IOptionsMonitor<JevOptions>>().CurrentValue.Enabled);
        var alert = Assert.Single(provider.GetRequiredService<JevAlerts>().Active);
        Assert.Equal(JevAlertKind.ClientFailed, alert.Kind);
        Assert.Equal(AlertSessionKey, alert.SessionKey);
        var response = await provider.GetRequiredService<IJevClient>().SystemOneAsync(JevTestRequests.StrategyLap60(), CancellationToken.None);
        Assert.Equal(JevResponseStatus.Disabled, response.Status);
    }

    [Fact]
    public void AddPitWallJevReal_ClientRealWithoutKey_HotToggleCannotEnable()
    {
        using var provider = BuildProvider("Real", apiKey: null);

        provider.GetRequiredService<JevRuntimeOverrides>().SetEnabled(true);

        Assert.False(provider.GetRequiredService<IOptionsMonitor<JevOptions>>().CurrentValue.Enabled);
    }

    [Fact]
    public async Task AddPitWallJevReal_ClientRealWithKey_ResolvesRealClientEnabled()
    {
        using var provider = BuildProvider("Real", apiKey: TypeSafeExamples.TestApiKey);

        await StartHostedServicesAsync(provider);

        Assert.True(provider.GetRequiredService<IOptionsMonitor<JevOptions>>().CurrentValue.Enabled);
        Assert.IsType<JevClient>(provider.GetRequiredKeyedService<IJevClient>(JevClientKind.Real));
        Assert.Empty(provider.GetRequiredService<JevAlerts>().Active);
        var http = provider.GetRequiredService<IHttpClientFactory>().CreateClient(JevClient.HttpClientName);
        Assert.Equal(new Uri("https://api.typesafe.ai/v1/"), http.BaseAddress);
    }

    [Fact]
    public async Task AddPitWallJevReal_ClientFakeWithoutKey_StaysEnabledWithoutAlert()
    {
        using var provider = BuildProvider("Fake", apiKey: null);

        await StartHostedServicesAsync(provider);

        Assert.True(provider.GetRequiredService<IOptionsMonitor<JevOptions>>().CurrentValue.Enabled);
        Assert.Empty(provider.GetRequiredService<JevAlerts>().Active);
    }

    [Fact]
    public void AddPitWallJevReal_BaseUrlWithoutTrailingSlash_KeepsVersionSegment()
    {
        using var provider = BuildProvider("Real", apiKey: TypeSafeExamples.TestApiKey, baseUrl: "https://api.typesafe.ai/v1");

        var http = provider.GetRequiredService<IHttpClientFactory>().CreateClient(JevClient.HttpClientName);

        Assert.Equal(new Uri("https://api.typesafe.ai/v1/systemone"), new Uri(http.BaseAddress!, "systemone"));
    }

    private static async Task StartHostedServicesAsync(IServiceProvider provider)
    {
        foreach (var service in provider.GetServices<IHostedService>())
        {
            await service.StartAsync(CancellationToken.None);
        }
    }

    private static ServiceProvider BuildProvider(string client, string? apiKey, string baseUrl = "https://api.typesafe.ai/v1/")
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jev:Client"] = client,
                ["Jev:ApiKey"] = apiKey,
                ["Jev:BaseUrl"] = baseUrl,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        services.AddSingleton<IMeterFactory, TestMeterFactory>();
        services.AddPitWallJev();
        services.AddPitWallJevReal(_ => AlertSessionKey);

        return services.BuildServiceProvider();
    }
}
