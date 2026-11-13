using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using PitWall.Data;
using PitWall.Ingestion.OpenF1;

namespace PitWall.Ingestion;

public static class IngestionServiceCollectionExtensions
{
    /// <summary>
    /// Registra <see cref="SessionIngestor"/>, el <see cref="OpenF1Client"/> tipado con pausa y reintentos,
    /// el grabador de fixtures y el SQLite de <see cref="IngestionOptions.DatabasePath"/>.
    /// </summary>
    public static IServiceCollection AddPitWallIngestion(this IServiceCollection services, IngestionOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        var openF1Options = new OpenF1Options { RequestDelay = options.RequestDelay };

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(options);
        services.AddSingleton(openF1Options);
        services.AddSingleton(new OpenF1FixtureRecorder(options.FixturesPath));
        services.AddTransient<OpenF1RateLimitHandler>();

        // El handler guarda cuándo fue el último request: vida infinita para que la pausa no se pierda al rotar handlers.
        services.AddHttpClient<OpenF1Client>(http => http.BaseAddress = openF1Options.BaseAddress)
            .AddHttpMessageHandler<OpenF1RateLimitHandler>()
            .SetHandlerLifetime(Timeout.InfiniteTimeSpan);

        var databasePath = Path.GetFullPath(options.DatabasePath);
        services.AddDbContextFactory<PitWallDbContext>(db => db.UseSqlite($"Data Source={databasePath}"));
        services.AddTransient<SessionIngestor>();

        services.AddLogging(logging => logging
            .AddFilter("System.Net.Http.HttpClient", LogLevel.Warning)
            .AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning)
            .AddSimpleConsole(console =>
            {
                console.SingleLine = true;
                console.TimestampFormat = "HH:mm:ss ";
            }));

        return services;
    }
}
