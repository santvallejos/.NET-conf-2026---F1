using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PitWall.Data;

/// <summary>
/// Contexto para <c>dotnet ef</c> (migraciones y <c>database update</c>) sobre <c>data/pitwall.db</c> en la raíz del repo.
/// </summary>
public sealed class PitWallDesignTimeDbContextFactory : IDesignTimeDbContextFactory<PitWallDbContext>
{
    public const string DefaultDatabasePath = "data/pitwall.db";

    private const string SolutionFileName = "PitWall.sln";

    public PitWallDbContext CreateDbContext(string[] args)
    {
        var databasePath = Path.Combine(FindRepositoryRoot(), DefaultDatabasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

        var options = new DbContextOptionsBuilder<PitWallDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        return new PitWallDbContext(options);
    }

    // dotnet ef ejecuta la factory con el directorio del proyecto como directorio actual;
    // se sube hasta la solución para que la base quede en data/ de la raíz.
    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, SolutionFileName)))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
