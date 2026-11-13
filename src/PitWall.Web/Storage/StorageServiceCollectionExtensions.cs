using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

using PitWall.Data;

namespace PitWall.Web.Storage;

public static class StorageServiceCollectionExtensions
{
    private const string DatabasePathKey = "PitWall:DatabasePath";
    private const string SolutionFileName = "PitWall.sln";

    /// <summary>
    /// Registra la fábrica de <see cref="PitWallDbContext"/> sobre la base SQLite existente.
    /// </summary>
    public static IServiceCollection AddPitWallStorage(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var databasePath = ResolveDatabasePath(
            configuration[DatabasePathKey] ?? PitWallDesignTimeDbContextFactory.DefaultDatabasePath,
            environment.ContentRootPath);

        // Mode=ReadWrite: la base la crean las migraciones/la ingesta; la app no crea un archivo vacío.
        services.AddDbContextFactory<PitWallDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath};Mode=ReadWrite"));
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }

    // Una ruta relativa se resuelve desde la raíz del repo, igual que la factory de dotnet ef, para que
    // Web lea la misma base aunque se ejecute con el directorio del proyecto como directorio actual.
    private static string ResolveDatabasePath(string configuredPath, string contentRootPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        var current = new DirectoryInfo(contentRootPath);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, SolutionFileName)))
            {
                return Path.Combine(current.FullName, configuredPath);
            }

            current = current.Parent;
        }

        return Path.Combine(contentRootPath, configuredPath);
    }
}
