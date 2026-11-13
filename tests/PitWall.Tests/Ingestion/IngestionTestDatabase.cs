using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using PitWall.Data;

namespace PitWall.Tests.Ingestion;

/// <summary>SQLite en memoria vacío; la conexión abierta mantiene la base viva entre contextos.</summary>
internal sealed class IngestionTestDatabase : IDbContextFactory<PitWallDbContext>, IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public IngestionTestDatabase()
    {
        _connection.Open();
    }

    public PitWallDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<PitWallDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();
}
