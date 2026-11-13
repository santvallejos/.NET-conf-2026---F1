using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using PitWall.Data;
using PitWall.Data.Entities;
using PitWall.Domain;

namespace PitWall.Tests.Data;

public sealed class PitWallDbContextTests : IDisposable
{
    private const int SessionKey = 11412;

    private static readonly DateTimeOffset RaceStart = new(2026, 11, 8, 17, 0, 0, TimeSpan.Zero);

    private static readonly string[] SchemaTables =
    [
        "CarData", "Driver", "IngestedSession", "InjectedEvent", "Interval", "JevCache", "JevDecisionLog",
        "Lap", "Location", "Overtake", "PitStop", "Position", "RaceControl", "Scenario", "SessionResult",
        "StartingGrid", "Stint", "TeamRadio", "Weather",
    ];

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public PitWallDbContextTests()
    {
        _connection.Open();
        using var context = CreateContext();
        context.Database.Migrate();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void Migrate_OnEmptyInMemoryDatabase_CreatesEverySchemaTable()
    {
        var tables = QueryStrings(
            "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE '\\_\\_EF%' ESCAPE '\\' AND name <> 'sqlite_sequence' ORDER BY name");

        Assert.Equal(SchemaTables, tables);
    }

    [Fact]
    public void HasPendingModelChanges_AfterInitialMigration_ReturnsFalse()
    {
        using var context = CreateContext();

        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void SaveChanges_OneRowPerTable_ReadsBackSameValues()
    {
        AssertRoundTrip(new IngestedSession
        {
            SessionKey = SessionKey,
            MeetingKey = 1299,
            Name = "Race",
            Circuit = "Interlagos",
            DateStart = RaceStart,
            DateEnd = RaceStart.AddHours(2),
            QualiSessionKey = 11408,
            IngestedAt = RaceStart.AddDays(1),
        });
        AssertRoundTrip(new Driver
        {
            SessionKey = SessionKey,
            DriverNumber = 43,
            FullName = "Franco Colapinto",
            Acronym = "COL",
            Team = "Alpine",
            TeamColour = "0093CC",
        });
        AssertRoundTrip(new Lap
        {
            SessionKey = SessionKey,
            DriverNumber = 43,
            LapNumber = 2,
            DateStart = RaceStart.AddMinutes(2),
            Duration = 74.512,
            S1 = 18.1,
            S2 = 38.2,
            S3 = 18.212,
            I1Speed = 301,
            I2Speed = 288,
            IsPitOutLap = false,
        });
        AssertRoundTrip(new Stint
        {
            SessionKey = SessionKey,
            DriverNumber = 43,
            StintNumber = 1,
            LapStart = 1,
            LapEnd = 24,
            Compound = Compound.Medium,
            TyreAgeAtStart = 0,
        });
        AssertRoundTrip(new PitStop
        {
            SessionKey = SessionKey,
            DriverNumber = 43,
            LapNumber = 24,
            Date = RaceStart.AddMinutes(31),
            PitDuration = 22.4,
            LaneDuration = 21.9,
            StopDuration = 2.4,
        });
        AssertRoundTrip(new RaceControl
        {
            SessionKey = SessionKey,
            Date = RaceStart.AddMinutes(40),
            LapNumber = 30,
            Category = "SafetyCar",
            Flag = null,
            Scope = "Track",
            DriverNumber = null,
            Message = "SAFETY CAR DEPLOYED",
        });
        AssertRoundTrip(new Weather
        {
            SessionKey = SessionKey,
            Date = RaceStart,
            AirTemp = 24.5,
            TrackTemp = 41.2,
            Humidity = 55,
            Rainfall = false,
            WindSpeed = 1.8,
            WindDirection = 210,
            Pressure = 925.3,
        });
        AssertRoundTrip(new IntervalSample
        {
            SessionKey = SessionKey,
            DriverNumber = 43,
            Date = RaceStart.AddMinutes(10),
            GapToLeader = 12.345,
            Interval = 0.9,
        });
        AssertRoundTrip(new PositionSample
        {
            SessionKey = SessionKey,
            DriverNumber = 43,
            Date = RaceStart.AddMinutes(10),
            Position = 13,
        });
        AssertRoundTrip(new Overtake
        {
            SessionKey = SessionKey,
            Date = RaceStart.AddMinutes(12),
            OvertakingDriver = 43,
            OvertakenDriver = 44,
            Position = 12,
        });
        AssertRoundTrip(new SessionResult
        {
            SessionKey = SessionKey,
            DriverNumber = 43,
            Position = 10,
            Laps = 71,
            Dnf = false,
            Dns = false,
            Dsq = false,
            Duration = 5520.1,
            GapToLeader = 48.7,
        });
        AssertRoundTrip(new StartingGrid
        {
            SessionKey = SessionKey,
            DriverNumber = 43,
            Position = 14,
            LapDuration = 70.1,
        });
        AssertRoundTrip(new TeamRadio
        {
            SessionKey = SessionKey,
            DriverNumber = 43,
            Date = RaceStart.AddMinutes(5),
            RecordingUrl = "https://example.invalid/radio.mp3",
            LocalPath = "data/radio/11412/radio.mp3",
        });
        AssertRoundTrip(new CarData
        {
            SessionKey = SessionKey,
            DriverNumber = 43,
            Date = RaceStart.AddSeconds(1.234567),
            Speed = 312,
            Rpm = 11500,
            Gear = 8,
            Throttle = 100,
            Brake = 0,
        });
        AssertRoundTrip(new Location
        {
            SessionKey = SessionKey,
            DriverNumber = 43,
            Date = RaceStart.AddSeconds(1.234567),
            X = -1520,
            Y = 830,
            Z = 12,
        });

        var scenario = new Scenario { SessionKey = SessionKey, Name = "Safety car vuelta 30", CreatedAt = RaceStart };
        AssertRoundTrip(scenario);
        AssertRoundTrip(new InjectedEvent
        {
            ScenarioId = scenario.Id,
            Lap = 30,
            Type = "SafetyCar",
            Params = """{"lap":30,"durationLaps":4}""",
            CreatedAt = RaceStart,
        });
        AssertRoundTrip(new JevDecisionLog
        {
            SessionKey = SessionKey,
            ScenarioId = scenario.Id,
            DriverNumber = 43,
            Lap = 30,
            Kind = JevKind.Strategy,
            StateHash = "abc123",
            RequestJson = "{}",
            ResponseJson = "{}",
            LatencyMs = 850,
            Tokens = 1200,
            CostUsd = 0.0042,
            CreatedAt = RaceStart,
        });
        AssertRoundTrip(new JevCache
        {
            StateHash = "abc123",
            Kind = JevKind.Triage,
            ResponseJson = "{}",
            CreatedAt = RaceStart,
        });
    }

    [Theory]
    [InlineData(Compound.Soft, "SOFT")]
    [InlineData(Compound.Medium, "MEDIUM")]
    [InlineData(Compound.Hard, "HARD")]
    [InlineData(Compound.Intermediate, "INTERMEDIATE")]
    [InlineData(Compound.Wet, "WET")]
    public void SaveChanges_StintCompound_PersistsAsUpperCaseText(Compound compound, string expected)
    {
        AssertRoundTrip(new Stint
        {
            SessionKey = SessionKey,
            DriverNumber = 43,
            StintNumber = 1,
            LapStart = 1,
            LapEnd = 20,
            Compound = compound,
            TyreAgeAtStart = 3,
        });

        Assert.Equal([expected], QueryStrings("SELECT Compound FROM Stint"));
        Assert.Equal(["text"], QueryStrings("SELECT typeof(Compound) FROM Stint"));
    }

    [Fact]
    public void Where_CarDataDateRange_FiltersAndOrdersInDatabase()
    {
        var local = TimeSpan.FromHours(-3);
        using (var context = CreateContext())
        {
            context.CarData.AddRange(
                NewCarData(RaceStart.AddSeconds(30).ToOffset(local)),
                NewCarData(RaceStart.AddSeconds(-30)),
                NewCarData(RaceStart.AddSeconds(10).ToOffset(local)),
                NewCarData(RaceStart.AddSeconds(90)));
            context.SaveChanges();
        }

        using var readContext = CreateContext();
        var from = RaceStart;
        var to = RaceStart.AddMinutes(1);
        var dates = readContext.CarData
            .Where(c => c.SessionKey == SessionKey && c.Date >= from && c.Date < to)
            .OrderBy(c => c.Date)
            .Select(c => c.Date)
            .ToList();

        Assert.Equal([RaceStart.AddSeconds(10), RaceStart.AddSeconds(30)], dates);
        Assert.All(dates, date => Assert.Equal(TimeSpan.Zero, date.Offset));
    }

    private static CarData NewCarData(DateTimeOffset date) => new()
    {
        SessionKey = SessionKey,
        DriverNumber = 43,
        Date = date,
        Speed = 300,
        Rpm = 11000,
        Gear = 7,
        Throttle = 100,
        Brake = 0,
    };

    private PitWallDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PitWallDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new PitWallDbContext(options);
    }

    private void AssertRoundTrip<TEntity>(TEntity entity)
        where TEntity : class
    {
        using (var context = CreateContext())
        {
            context.Add(entity);
            context.SaveChanges();
        }

        using var readContext = CreateContext();
        var stored = Assert.Single(readContext.Set<TEntity>().AsNoTracking());
        Assert.Equivalent(entity, stored, strict: true);
    }

    private List<string> QueryStrings(string sql)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();

        var values = new List<string>();
        while (reader.Read())
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }
}
