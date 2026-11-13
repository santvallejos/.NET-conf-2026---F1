using Microsoft.EntityFrameworkCore;

using PitWall.Data.Converters;
using PitWall.Data.Entities;
using PitWall.Domain;

namespace PitWall.Data;

/// <summary>
/// Esquema SQLite de PitWall: tablas ingeridas desde OpenF1 y tablas generadas por la app.
/// Las tablas ingeridas usan su clave natural como PK (la ingesta hace upsert por ella) y no tienen
/// FK hacia <see cref="IngestedSession"/>, que se marca al final de la ingesta.
/// </summary>
public sealed class PitWallDbContext(DbContextOptions<PitWallDbContext> options) : DbContext(options)
{
    public DbSet<IngestedSession> IngestedSessions => Set<IngestedSession>();

    public DbSet<Driver> Drivers => Set<Driver>();

    public DbSet<Lap> Laps => Set<Lap>();

    public DbSet<Stint> Stints => Set<Stint>();

    public DbSet<PitStop> PitStops => Set<PitStop>();

    public DbSet<RaceControl> RaceControl => Set<RaceControl>();

    public DbSet<Weather> Weather => Set<Weather>();

    public DbSet<IntervalSample> Intervals => Set<IntervalSample>();

    public DbSet<PositionSample> Positions => Set<PositionSample>();

    public DbSet<Overtake> Overtakes => Set<Overtake>();

    public DbSet<SessionResult> SessionResults => Set<SessionResult>();

    public DbSet<StartingGrid> StartingGrid => Set<StartingGrid>();

    public DbSet<TeamRadio> TeamRadio => Set<TeamRadio>();

    public DbSet<CarData> CarData => Set<CarData>();

    public DbSet<Location> Locations => Set<Location>();

    public DbSet<Scenario> Scenarios => Set<Scenario>();

    public DbSet<InjectedEvent> InjectedEvents => Set<InjectedEvent>();

    public DbSet<JevDecisionLog> JevDecisionLog => Set<JevDecisionLog>();

    public DbSet<JevCache> JevCache => Set<JevCache>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcDateTimeOffsetConverter>();
        configurationBuilder.Properties<Compound>().HaveConversion<CompoundConverter>();
        configurationBuilder.Properties<JevKind>().HaveConversion<string>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IngestedSession>(entity =>
        {
            entity.ToTable(nameof(IngestedSession));
            entity.HasKey(e => e.SessionKey);
            entity.Property(e => e.SessionKey).ValueGeneratedNever();
        });

        modelBuilder.Entity<Driver>(entity =>
        {
            entity.ToTable(nameof(Driver));
            entity.HasKey(e => new { e.SessionKey, e.DriverNumber });
        });

        modelBuilder.Entity<Lap>(entity =>
        {
            entity.ToTable(nameof(Lap));
            entity.HasKey(e => new { e.SessionKey, e.DriverNumber, e.LapNumber });
        });

        modelBuilder.Entity<Stint>(entity =>
        {
            entity.ToTable(nameof(Stint));
            entity.HasKey(e => new { e.SessionKey, e.DriverNumber, e.StintNumber });
        });

        modelBuilder.Entity<PitStop>(entity =>
        {
            entity.ToTable(nameof(PitStop));
            entity.HasKey(e => new { e.SessionKey, e.DriverNumber, e.LapNumber });
        });

        modelBuilder.Entity<RaceControl>(entity =>
        {
            entity.ToTable(nameof(RaceControl));
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<Weather>(entity =>
        {
            entity.ToTable(nameof(Weather));
            entity.HasKey(e => new { e.SessionKey, e.Date });
        });

        modelBuilder.Entity<IntervalSample>(entity =>
        {
            entity.ToTable("Interval");
            entity.HasKey(e => new { e.SessionKey, e.DriverNumber, e.Date });
        });

        modelBuilder.Entity<PositionSample>(entity =>
        {
            entity.ToTable("Position");
            entity.HasKey(e => new { e.SessionKey, e.DriverNumber, e.Date });
        });

        modelBuilder.Entity<Overtake>(entity =>
        {
            entity.ToTable(nameof(Overtake));
            entity.HasKey(e => new { e.SessionKey, e.Date, e.OvertakingDriver, e.OvertakenDriver });
        });

        modelBuilder.Entity<SessionResult>(entity =>
        {
            entity.ToTable(nameof(SessionResult));
            entity.HasKey(e => new { e.SessionKey, e.DriverNumber });
        });

        modelBuilder.Entity<StartingGrid>(entity =>
        {
            entity.ToTable(nameof(StartingGrid));
            entity.HasKey(e => new { e.SessionKey, e.DriverNumber });
        });

        modelBuilder.Entity<TeamRadio>(entity =>
        {
            entity.ToTable(nameof(TeamRadio));
            entity.HasKey(e => new { e.SessionKey, e.DriverNumber, e.Date });
        });

        // Telemetría (~23.000 filas por piloto): la PK compuesta es el índice (SessionKey, DriverNumber, Date)
        // que usan el replay y la ingesta.
        modelBuilder.Entity<CarData>(entity =>
        {
            entity.ToTable(nameof(CarData));
            entity.HasKey(e => new { e.SessionKey, e.DriverNumber, e.Date });
        });

        modelBuilder.Entity<Location>(entity =>
        {
            entity.ToTable(nameof(Location));
            entity.HasKey(e => new { e.SessionKey, e.DriverNumber, e.Date });
        });

        modelBuilder.Entity<Scenario>(entity =>
        {
            entity.ToTable(nameof(Scenario));
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<InjectedEvent>(entity =>
        {
            entity.ToTable(nameof(InjectedEvent));
            entity.HasKey(e => e.Id);
            entity.HasOne<Scenario>()
                .WithMany()
                .HasForeignKey(e => e.ScenarioId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JevDecisionLog>(entity =>
        {
            entity.ToTable(nameof(JevDecisionLog));
            entity.HasKey(e => e.Id);
            entity.HasOne<Scenario>()
                .WithMany()
                .HasForeignKey(e => e.ScenarioId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<JevCache>(entity =>
        {
            entity.ToTable(nameof(JevCache));
            entity.HasKey(e => e.StateHash);
        });
    }
}
