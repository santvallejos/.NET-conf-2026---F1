using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using PitWall.Data;
using PitWall.Data.Entities;
using PitWall.Domain;

namespace PitWall.Tests.Replay;

/// <summary>
/// SQLite en memoria con una carrera sintética de 4 vueltas y 3 autos (más un inscripto sin datos).
/// Vueltas del líder (12) cada 90 s desde <see cref="RaceStart"/>; el 43 entra a boxes en la vuelta 2
/// (pit lane de +170 s a +192 s) y sale con HARD en la vuelta 3, que inicia a +200 s.
/// </summary>
public sealed class ReplayTestDatabase : IDbContextFactory<PitWallDbContext>, IDisposable
{
    public const int SessionKey = 11412;

    public static readonly DateTimeOffset RaceStart = new(2026, 11, 8, 17, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public ReplayTestDatabase(bool seed = true)
    {
        _connection.Open();
        using var context = CreateDbContext();
        context.Database.Migrate();
        if (seed)
        {
            Seed(context);
        }
    }

    public static DateTimeOffset At(double seconds) => RaceStart.AddSeconds(seconds);

    public PitWallDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<PitWallDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();

    private static void Seed(PitWallDbContext context)
    {
        foreach (var number in new[] { 12, 43, 44, 99 })
        {
            context.Drivers.Add(new Driver { SessionKey = SessionKey, DriverNumber = number, FullName = $"Driver {number}", Acronym = $"D{number}", Team = "Team", TeamColour = "FFFFFF" });
        }

        AddLaps(context, 12, 0, 90, 180, 270);
        AddLaps(context, 44, 0, 91, 182, 273);
        AddLaps(context, 43, 0, 92, 200, 292);

        context.Stints.AddRange(
            new Stint { SessionKey = SessionKey, DriverNumber = 12, StintNumber = 1, LapStart = 1, LapEnd = 4, Compound = Compound.Medium, TyreAgeAtStart = 0 },
            new Stint { SessionKey = SessionKey, DriverNumber = 44, StintNumber = 1, LapStart = 1, LapEnd = 4, Compound = Compound.Hard, TyreAgeAtStart = 2 },
            new Stint { SessionKey = SessionKey, DriverNumber = 43, StintNumber = 1, LapStart = 1, LapEnd = 2, Compound = Compound.Soft, TyreAgeAtStart = 0 },
            new Stint { SessionKey = SessionKey, DriverNumber = 43, StintNumber = 2, LapStart = 3, LapEnd = 4, Compound = Compound.Hard, TyreAgeAtStart = 0 });

        context.PitStops.Add(new PitStop { SessionKey = SessionKey, DriverNumber = 43, LapNumber = 2, Date = At(170), PitDuration = 24.0, LaneDuration = 22.0, StopDuration = 2.4 });

        context.StartingGrid.AddRange(
            new StartingGrid { SessionKey = SessionKey, DriverNumber = 44, Position = 1 },
            new StartingGrid { SessionKey = SessionKey, DriverNumber = 12, Position = 2 },
            new StartingGrid { SessionKey = SessionKey, DriverNumber = 43, Position = 3 });

        context.Positions.AddRange(
            new PositionSample { SessionKey = SessionKey, DriverNumber = 43, Date = At(60), Position = 2 },
            new PositionSample { SessionKey = SessionKey, DriverNumber = 44, Date = At(60), Position = 3 },
            new PositionSample { SessionKey = SessionKey, DriverNumber = 12, Date = At(60), Position = 1 },
            new PositionSample { SessionKey = SessionKey, DriverNumber = 43, Date = At(185), Position = 3 },
            new PositionSample { SessionKey = SessionKey, DriverNumber = 44, Date = At(185), Position = 2 });

        context.Intervals.AddRange(
            new IntervalSample { SessionKey = SessionKey, DriverNumber = 12, Date = At(150), GapToLeader = 0, Interval = 0 },
            new IntervalSample { SessionKey = SessionKey, DriverNumber = 43, Date = At(150), GapToLeader = 1.2, Interval = 1.2 },
            new IntervalSample { SessionKey = SessionKey, DriverNumber = 44, Date = At(150), GapToLeader = 2.0, Interval = 0.8 },
            new IntervalSample { SessionKey = SessionKey, DriverNumber = 43, Date = At(195), GapToLeader = 21.5, Interval = 19.5 },
            new IntervalSample { SessionKey = SessionKey, DriverNumber = 44, Date = At(195), GapToLeader = 2.0, Interval = 2.0 });

        context.Weather.AddRange(
            new Weather { SessionKey = SessionKey, Date = At(-60), AirTemp = 25.0, TrackTemp = 40.0, Humidity = 50, Rainfall = false, WindSpeed = 1.0, WindDirection = 90, Pressure = 1013 },
            new Weather { SessionKey = SessionKey, Date = At(120), AirTemp = 24.5, TrackTemp = 39.0, Humidity = 55, Rainfall = true, WindSpeed = 1.2, WindDirection = 95, Pressure = 1012 });

        context.RaceControl.AddRange(
            new RaceControl { Id = 1, SessionKey = SessionKey, Date = At(-120), Category = "Flag", Flag = "GREEN", Scope = "Track", Message = "GREEN LIGHT - PIT EXIT OPEN" },
            new RaceControl { Id = 2, SessionKey = SessionKey, Date = At(100), LapNumber = 2, Category = "Flag", Flag = "YELLOW", Scope = "Sector", Message = "YELLOW IN TRACK SECTOR 5" },
            new RaceControl { Id = 3, SessionKey = SessionKey, Date = At(150), LapNumber = 2, Category = "SafetyCar", Scope = "Track", Message = "VIRTUAL SAFETY CAR DEPLOYED" },
            new RaceControl { Id = 4, SessionKey = SessionKey, Date = At(190), LapNumber = 3, Category = "SafetyCar", Scope = "Track", Message = "VIRTUAL SAFETY CAR ENDING" },
            new RaceControl { Id = 5, SessionKey = SessionKey, Date = At(210), LapNumber = 3, Category = "Flag", Flag = "GREEN", Scope = "Track", Message = "TRACK CLEAR" });

        context.SaveChanges();
    }

    private static void AddLaps(PitWallDbContext context, int driverNumber, params double[] lapStarts)
    {
        for (var index = 0; index < lapStarts.Length; index++)
        {
            var end = index + 1 < lapStarts.Length ? lapStarts[index + 1] : lapStarts[index] + 90;
            context.Laps.Add(new Lap
            {
                SessionKey = SessionKey,
                DriverNumber = driverNumber,
                LapNumber = index + 1,
                DateStart = At(lapStarts[index]),
                Duration = index == 0 ? null : end - lapStarts[index],
            });
        }
    }
}
