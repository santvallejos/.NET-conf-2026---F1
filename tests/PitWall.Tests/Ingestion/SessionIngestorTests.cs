using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using PitWall.Data;
using PitWall.Domain;
using PitWall.Ingestion;
using PitWall.Ingestion.OpenF1;

namespace PitWall.Tests.Ingestion;

/// <summary>
/// Ingesta contra fixtures grabados de OpenF1 (Brasil 2026, recortados a los pilotos 43 y 12) sin red.
/// Telemetría solo del 43: hay fixtures para las ventanas 17:00 y 17:10; el resto responde 404 (sin datos).
/// </summary>
public sealed class SessionIngestorTests : IDisposable
{
    private const int SessionKey = 11412;
    private const int QualiSessionKey = 11408;

    private static readonly DateTimeOffset Now = new(2026, 11, 12, 22, 0, 0, TimeSpan.Zero);

    private readonly IngestionTestDatabase _database = new();
    private readonly OpenF1FixtureHandler _handler = new();
    private readonly string _workDirectory = Path.Combine(Path.GetTempPath(), $"pitwall-ingestion-{Guid.NewGuid():N}");

    public void Dispose()
    {
        _database.Dispose();
        if (Directory.Exists(_workDirectory))
        {
            Directory.Delete(_workDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task IngestAsync_WithRecordedFixtures_WritesOneRowPerFixtureRow()
    {
        var report = await IngestAsync(Options());

        await using var context = _database.CreateDbContext();
        Assert.Equal(2, await context.Drivers.CountAsync());
        Assert.Equal(5, await context.Laps.CountAsync());
        Assert.Equal(8, await context.Stints.CountAsync());
        Assert.Equal(6, await context.PitStops.CountAsync());
        Assert.Equal(5, await context.RaceControl.CountAsync());
        Assert.Equal(3, await context.Weather.CountAsync());
        Assert.Equal(4, await context.Intervals.CountAsync());
        Assert.Equal(4, await context.Positions.CountAsync());
        Assert.Equal(3, await context.Overtakes.CountAsync());
        Assert.Equal(2, await context.SessionResults.CountAsync());
        Assert.Equal(2, await context.StartingGrid.CountAsync());
        Assert.Equal(2, await context.TeamRadio.CountAsync());
        Assert.Equal(15, await context.CarData.CountAsync());
        Assert.Equal(12, await context.Locations.CountAsync());
        Assert.Equal(15, report.CarData);
        Assert.Equal(12, report.Locations);
    }

    [Fact]
    public async Task IngestAsync_WithRecordedFixtures_MapsOpenF1Fields()
    {
        await IngestAsync(Options());

        await using var context = _database.CreateDbContext();
        var colapinto = await context.Drivers.SingleAsync(row => row.DriverNumber == 43);
        Assert.Equal(("Franco COLAPINTO", "COL", "Alpine", "00A1E8"), (colapinto.FullName, colapinto.Acronym, colapinto.Team, colapinto.TeamColour));

        var lap2 = await context.Laps.SingleAsync(row => row.DriverNumber == 43 && row.LapNumber == 2);
        Assert.Equal(new DateTimeOffset(2026, 11, 8, 17, 5, 4, 991, TimeSpan.Zero), lap2.DateStart);
        Assert.Equal(((double?)23.69, (double?)47.949, (double?)19.439, (int?)283, (int?)191), (lap2.S1, lap2.S2, lap2.S3, lap2.I1Speed, lap2.I2Speed));

        var stint = await context.Stints.SingleAsync(row => row.DriverNumber == 43 && row.StintNumber == 2);
        Assert.Equal((18, 53, Compound.Soft, 0), (stint.LapStart, stint.LapEnd, stint.Compound, stint.TyreAgeAtStart));

        var pit = await context.PitStops.SingleAsync(row => row.DriverNumber == 43 && row.LapNumber == 53);
        Assert.Equal(23.652, pit.LaneDuration);
        Assert.Null(pit.StopDuration);

        var lapped = await context.Intervals.SingleAsync(row => row.DriverNumber == 43 && row.Interval == 0.493);
        Assert.Null(lapped.GapToLeader);

        var result = await context.SessionResults.SingleAsync(row => row.DriverNumber == 43);
        Assert.Equal(((int?)13, 70, false), (result.Position, result.Laps, result.Dnf));
        Assert.Null(result.GapToLeader);
        Assert.Null(result.Duration);

        var weather = await context.Weather.OrderBy(row => row.Date).FirstAsync();
        Assert.Equal((16.3, 43.6, 88.7, false, 178, 930.0), (weather.AirTemp, weather.TrackTemp, weather.Humidity, weather.Rainfall, weather.WindDirection, weather.Pressure));

        var overtakeDisabled = await context.RaceControl.SingleAsync(row => row.Message == "OVERTAKE DISABLED");
        Assert.Equal(("Other", (string?)null, (int?)1, (int?)null), (overtakeDisabled.Category, overtakeDisabled.Flag, overtakeDisabled.LapNumber, overtakeDisabled.DriverNumber));

        var firstSample = await context.CarData.OrderBy(row => row.Date).FirstAsync();
        Assert.Equal((43, 4007, 0, 0, 0, 0), (firstSample.DriverNumber, firstSample.Rpm, firstSample.Speed, firstSample.Gear, firstSample.Throttle, firstSample.Brake));

        var location = await context.Locations.OrderBy(row => row.Date).LastAsync();
        Assert.True(location.Date >= new DateTimeOffset(2026, 11, 8, 17, 10, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task IngestAsync_WithQuali_ReadsStartingGridFromQualiAndStoresItUnderRaceSession()
    {
        await IngestAsync(Options());

        Assert.Contains(_handler.Requests, uri => uri.AbsolutePath.EndsWith("/starting_grid", StringComparison.Ordinal) && uri.Query == $"?session_key={QualiSessionKey}");
        Assert.DoesNotContain(_handler.Requests, uri => uri.AbsolutePath.EndsWith("/starting_grid", StringComparison.Ordinal) && uri.Query.Contains($"{SessionKey}", StringComparison.Ordinal));

        await using var context = _database.CreateDbContext();
        var grid = await context.StartingGrid.OrderBy(row => row.Position).ToListAsync();
        Assert.All(grid, row => Assert.Equal(SessionKey, row.SessionKey));
        Assert.Equal([(12, 2, 88.095), (43, 13, 93.327)], grid.Select(row => (row.DriverNumber, row.Position, row.LapDuration!.Value)));
    }

    [Fact]
    public async Task IngestAsync_WithoutQuali_SkipsStartingGrid()
    {
        var report = await IngestAsync(Options() with { QualiSessionKey = null });

        Assert.Equal(0, report.StartingGrid);
        Assert.DoesNotContain(_handler.Requests, uri => uri.AbsolutePath.EndsWith("/starting_grid", StringComparison.Ordinal));
    }

    [Fact]
    public async Task IngestAsync_WithDrivers_RequestsTelemetryOnlyForThoseDriversInTenMinuteWindowsOfTheRace()
    {
        await IngestAsync(Options());

        string[] sessionEndpoints =
        [
            "sessions", "drivers", "laps", "stints", "pit", "race_control", "weather", "intervals",
            "position", "overtakes", "session_result", "team_radio",
        ];
        foreach (var endpoint in sessionEndpoints)
        {
            Assert.Contains(_handler.Requests, uri => uri.AbsolutePath.EndsWith($"/{endpoint}", StringComparison.Ordinal) && uri.Query == $"?session_key={SessionKey}");
        }

        var raceStart = new DateTimeOffset(2026, 11, 8, 17, 0, 0, TimeSpan.Zero);
        var expectedWindows = Enumerable.Range(0, 12)
            .Select(index => OpenF1Client.BuildRequestUri("car_data", SessionKey, 43, new TelemetryWindow(raceStart.AddMinutes(10 * index), raceStart.AddMinutes(10 * (index + 1)))))
            .Select(uri => new Uri(OpenF1Options.DefaultBaseAddress, uri).Query);
        var carDataRequests = _handler.Requests.Where(uri => uri.AbsolutePath.EndsWith("/car_data", StringComparison.Ordinal)).Select(uri => uri.Query);
        Assert.Equal(expectedWindows, carDataRequests);
        Assert.Equal(12, _handler.Requests.Count(uri => uri.AbsolutePath.EndsWith("/location", StringComparison.Ordinal) && uri.Query.Contains("driver_number=43", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task IngestAsync_WithoutDrivers_DownloadsNoTelemetry()
    {
        var report = await IngestAsync(Options() with { Drivers = [] });

        Assert.Equal((0, 0), (report.CarData, report.Locations));
        Assert.DoesNotContain(_handler.Requests, uri => uri.AbsolutePath.EndsWith("/car_data", StringComparison.Ordinal) || uri.AbsolutePath.EndsWith("/location", StringComparison.Ordinal));
    }

    [Fact]
    public async Task IngestAsync_UnknownDriver_ThrowsBeforeWriting()
    {
        await Assert.ThrowsAsync<OpenF1IngestionException>(() => IngestAsync(Options() with { Drivers = [43, 99] }));

        await using var context = _database.CreateDbContext();
        await context.Database.MigrateAsync();
        Assert.Equal(0, await context.Drivers.CountAsync());
    }

    [Fact]
    public async Task IngestAsync_WithTeamRadio_SavesMp3UnderSessionFolder()
    {
        var options = Options();
        _handler.MissingRecordings.Add("ANT_12_20261108_141229.mp3");

        var report = await IngestAsync(options);

        var file = Path.Combine(options.RadioPath, "11412", "ANT_12_20261108_132331.mp3");
        Assert.Equal(OpenF1FixtureHandler.RecordingBytes, await File.ReadAllBytesAsync(file));
        Assert.Equal((1, 1), (report.RadioFilesDownloaded, report.RadioFilesMissing));

        await using var context = _database.CreateDbContext();
        var radio = await context.TeamRadio.OrderBy(row => row.Date).ToListAsync();
        Assert.Equal(["11412/ANT_12_20261108_132331.mp3", "11412/ANT_12_20261108_141229.mp3"], radio.Select(row => row.LocalPath));
        Assert.StartsWith("https://livetiming.formula1.com/", radio[0].RecordingUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IngestAsync_RunTwice_DoesNotDuplicateRowsNorRedownloadMp3()
    {
        var options = Options();
        var first = await IngestAsync(options);
        var requestsAfterFirstRun = _handler.Requests.Count;

        var second = await IngestAsync(options);

        Assert.Equal(first with { RadioFilesDownloaded = 0 }, second);
        Assert.DoesNotContain(_handler.Requests.Skip(requestsAfterFirstRun), uri => uri.Host != OpenF1Options.DefaultBaseAddress.Host);

        await using var context = _database.CreateDbContext();
        Assert.Equal(5, await context.Laps.CountAsync());
        Assert.Equal(5, await context.RaceControl.CountAsync());
        Assert.Equal(15, await context.CarData.CountAsync());
        Assert.Equal(1, await context.IngestedSessions.CountAsync());
    }

    [Fact]
    public async Task IngestAsync_Completed_RecordsIngestedSession()
    {
        await IngestAsync(Options());

        await using var context = _database.CreateDbContext();
        var session = await context.IngestedSessions.SingleAsync();
        Assert.Equal(SessionKey, session.SessionKey);
        Assert.Equal(1299, session.MeetingKey);
        Assert.Equal("São Paulo 2026 Race", session.Name);
        Assert.Equal("Interlagos", session.Circuit);
        Assert.Equal(new DateTimeOffset(2026, 11, 8, 17, 0, 0, TimeSpan.Zero), session.DateStart);
        Assert.Equal(new DateTimeOffset(2026, 11, 8, 19, 0, 0, TimeSpan.Zero), session.DateEnd);
        Assert.Equal(QualiSessionKey, session.QualiSessionKey);
        Assert.Equal(Now, session.IngestedAt);
    }

    [Fact]
    public async Task IngestAsync_RecordFixtures_WritesEachResponseWithTheNameTheStubServes()
    {
        var recorded = Path.Combine(_workDirectory, "fixtures");

        await IngestAsync(Options(), recorded);

        var expected = Directory.GetFiles(OpenF1Fixtures.Directory).Select(Path.GetFileName).Order();
        Assert.Equal(expected, Directory.GetFiles(recorded).Select(Path.GetFileName).Order());
        Assert.Equal(
            await File.ReadAllTextAsync(Path.Combine(OpenF1Fixtures.Directory, "pit_11412.json")),
            await File.ReadAllTextAsync(Path.Combine(recorded, "pit_11412.json")));
    }

    private IngestionOptions Options() => new()
    {
        SessionKey = SessionKey,
        QualiSessionKey = QualiSessionKey,
        Drivers = [43],
        RadioPath = Path.Combine(_workDirectory, "radio"),
        RequestDelay = TimeSpan.Zero,
    };

    private async Task<IngestionReport> IngestAsync(IngestionOptions options, string? fixturesPath = null)
    {
        var openF1Options = new OpenF1Options { RequestDelay = options.RequestDelay };
        using var http = new HttpClient(new OpenF1RateLimitHandler(openF1Options, TimeProvider.System, NullLogger<OpenF1RateLimitHandler>.Instance)
        {
            InnerHandler = _handler,
        }, disposeHandler: false)
        {
            BaseAddress = openF1Options.BaseAddress,
        };
        var client = new OpenF1Client(http, new OpenF1FixtureRecorder(fixturesPath));
        var ingestor = new SessionIngestor(client, _database, new FakeTimeProvider(Now), NullLogger<SessionIngestor>.Instance);

        return await ingestor.IngestAsync(options, CancellationToken.None);
    }
}
