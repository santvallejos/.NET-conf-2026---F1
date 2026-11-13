using System.Globalization;
using System.Text.Json.Serialization.Metadata;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using PitWall.Data;
using PitWall.Data.Entities;
using PitWall.Ingestion.OpenF1;

namespace PitWall.Ingestion;

/// <summary>
/// Descarga una sesión de OpenF1 a SQLite y sus mp3 de <c>team_radio</c> a disco.
/// Primero baja todo a memoria y después reemplaza los datos de la sesión en una sola transacción: una ingesta
/// que falla a mitad deja intacta la anterior. Cada tabla se deduplica por su clave natural (la PK), así que
/// re-ejecutar la misma sesión deja las mismas filas.
/// </summary>
public sealed partial class SessionIngestor(
    OpenF1Client client,
    IDbContextFactory<PitWallDbContext> dbContextFactory,
    TimeProvider timeProvider,
    ILogger<SessionIngestor> logger)
{
    public static readonly TimeSpan TelemetryWindowSize = TimeSpan.FromMinutes(10);

    private const int InsertBatchSize = 5000;

    private static readonly OpenF1JsonContext Json = OpenF1JsonContext.Default;

    public async Task<IngestionReport> IngestAsync(IngestionOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        var key = options.SessionKey;
        var session = (await client.GetAsync("sessions", key, Json.ListOpenF1Session, cancellationToken).ConfigureAwait(false))
            .FirstOrDefault()
            ?? throw new OpenF1IngestionException($"OpenF1 no tiene la sesión {key}.");

        var drivers = Dedupe(
            (await GetAsync("drivers", key, Json.ListOpenF1Driver, cancellationToken).ConfigureAwait(false)).Select(row => row.ToEntity(key)),
            row => row.DriverNumber);
        var unknownDrivers = options.Drivers.Except(drivers.Select(driver => driver.DriverNumber)).ToList();
        if (unknownDrivers.Count > 0)
        {
            throw new OpenF1IngestionException($"Los pilotos {string.Join(',', unknownDrivers)} no corren la sesión {key}.");
        }

        var laps = Dedupe(
            (await GetAsync("laps", key, Json.ListOpenF1Lap, cancellationToken).ConfigureAwait(false)).Select(row => row.ToEntity(key)),
            row => (row.DriverNumber, row.LapNumber));
        var stints = Dedupe(
            await GetStintsAsync(key, cancellationToken).ConfigureAwait(false),
            row => (row.DriverNumber, row.StintNumber));
        var pitStops = Dedupe(
            (await GetAsync("pit", key, Json.ListOpenF1Pit, cancellationToken).ConfigureAwait(false)).Select(row => row.ToEntity(key)),
            row => (row.DriverNumber, row.LapNumber));
        var raceControl = (await GetAsync("race_control", key, Json.ListOpenF1RaceControl, cancellationToken).ConfigureAwait(false))
            .Select(row => row.ToEntity(key))
            .ToList();
        var weather = Dedupe(
            (await GetAsync("weather", key, Json.ListOpenF1Weather, cancellationToken).ConfigureAwait(false)).Select(row => row.ToEntity(key)),
            row => row.Date);
        var intervals = Dedupe(
            (await GetAsync("intervals", key, Json.ListOpenF1Interval, cancellationToken).ConfigureAwait(false)).Select(row => row.ToEntity(key)),
            row => (row.DriverNumber, row.Date));
        var positions = Dedupe(
            (await GetAsync("position", key, Json.ListOpenF1Position, cancellationToken).ConfigureAwait(false)).Select(row => row.ToEntity(key)),
            row => (row.DriverNumber, row.Date));
        var overtakes = Dedupe(
            (await GetAsync("overtakes", key, Json.ListOpenF1Overtake, cancellationToken).ConfigureAwait(false)).Select(row => row.ToEntity(key)),
            row => (row.Date, row.OvertakingDriver, row.OvertakenDriver));
        var sessionResults = Dedupe(
            (await GetAsync("session_result", key, Json.ListOpenF1SessionResult, cancellationToken).ConfigureAwait(false)).Select(row => row.ToEntity(key)),
            row => row.DriverNumber);
        var startingGrid = Dedupe(await GetStartingGridAsync(key, options.QualiSessionKey, cancellationToken).ConfigureAwait(false), row => row.DriverNumber);
        var teamRadioRows = await GetAsync("team_radio", key, Json.ListOpenF1TeamRadio, cancellationToken).ConfigureAwait(false);

        var windows = TelemetryWindow.Split(session.DateStart, session.DateEnd, TelemetryWindowSize);
        var carData = new List<CarData>();
        var locations = new List<Location>();
        foreach (var driverNumber in options.Drivers)
        {
            foreach (var window in windows)
            {
                carData.AddRange((await client.GetAsync("car_data", key, Json.ListOpenF1CarData, cancellationToken, driverNumber, window).ConfigureAwait(false))
                    .Select(row => row.ToEntity(key)));
                locations.AddRange((await client.GetAsync("location", key, Json.ListOpenF1Location, cancellationToken, driverNumber, window).ConfigureAwait(false))
                    .Select(row => row.ToEntity(key)));
            }

            LogTelemetry(logger, driverNumber, windows.Count);
        }

        carData = Dedupe(carData, row => (row.DriverNumber, row.Date));
        locations = Dedupe(locations, row => (row.DriverNumber, row.Date));

        var radio = await DownloadTeamRadioAsync(key, teamRadioRows, options.RadioPath, cancellationToken).ConfigureAwait(false);

        var ingestedSession = new IngestedSession
        {
            SessionKey = key,
            MeetingKey = session.MeetingKey,
            Name = string.Join(' ', new[] { session.Location, session.Year > 0 ? session.Year.ToString(CultureInfo.InvariantCulture) : null, session.SessionName }
                .Where(part => !string.IsNullOrWhiteSpace(part))),
            Circuit = session.CircuitShortName ?? string.Empty,
            DateStart = session.DateStart,
            DateEnd = session.DateEnd,
            QualiSessionKey = options.QualiSessionKey,
            IngestedAt = timeProvider.GetUtcNow(),
        };

        await using (var context = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false))
        {
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            context.ChangeTracker.AutoDetectChangesEnabled = false;
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            await ReplaceAsync(context, context.Drivers.Where(row => row.SessionKey == key), drivers, cancellationToken).ConfigureAwait(false);
            await ReplaceAsync(context, context.Laps.Where(row => row.SessionKey == key), laps, cancellationToken).ConfigureAwait(false);
            await ReplaceAsync(context, context.Stints.Where(row => row.SessionKey == key), stints, cancellationToken).ConfigureAwait(false);
            await ReplaceAsync(context, context.PitStops.Where(row => row.SessionKey == key), pitStops, cancellationToken).ConfigureAwait(false);
            await ReplaceAsync(context, context.RaceControl.Where(row => row.SessionKey == key), raceControl, cancellationToken).ConfigureAwait(false);
            await ReplaceAsync(context, context.Weather.Where(row => row.SessionKey == key), weather, cancellationToken).ConfigureAwait(false);
            await ReplaceAsync(context, context.Intervals.Where(row => row.SessionKey == key), intervals, cancellationToken).ConfigureAwait(false);
            await ReplaceAsync(context, context.Positions.Where(row => row.SessionKey == key), positions, cancellationToken).ConfigureAwait(false);
            await ReplaceAsync(context, context.Overtakes.Where(row => row.SessionKey == key), overtakes, cancellationToken).ConfigureAwait(false);
            await ReplaceAsync(context, context.SessionResults.Where(row => row.SessionKey == key), sessionResults, cancellationToken).ConfigureAwait(false);
            await ReplaceAsync(context, context.StartingGrid.Where(row => row.SessionKey == key), startingGrid, cancellationToken).ConfigureAwait(false);
            await ReplaceAsync(context, context.TeamRadio.Where(row => row.SessionKey == key), radio.Rows, cancellationToken).ConfigureAwait(false);

            // La telemetría solo se reemplaza para los pilotos pedidos: la de otros pilotos de una ingesta anterior queda.
            var telemetryDrivers = options.Drivers.ToList();
            await ReplaceAsync(context, context.CarData.Where(row => row.SessionKey == key && telemetryDrivers.Contains(row.DriverNumber)), carData, cancellationToken).ConfigureAwait(false);
            await ReplaceAsync(context, context.Locations.Where(row => row.SessionKey == key && telemetryDrivers.Contains(row.DriverNumber)), locations, cancellationToken).ConfigureAwait(false);

            await ReplaceAsync(context, context.IngestedSessions.Where(row => row.SessionKey == key), [ingestedSession], cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return new IngestionReport
        {
            SessionKey = key,
            Drivers = drivers.Count,
            Laps = laps.Count,
            Stints = stints.Count,
            PitStops = pitStops.Count,
            RaceControl = raceControl.Count,
            Weather = weather.Count,
            Intervals = intervals.Count,
            Positions = positions.Count,
            Overtakes = overtakes.Count,
            SessionResults = sessionResults.Count,
            StartingGrid = startingGrid.Count,
            TeamRadio = radio.Rows.Count,
            CarData = carData.Count,
            Locations = locations.Count,
            RadioFilesDownloaded = radio.Downloaded,
            RadioFilesMissing = radio.Missing,
        };
    }

    private async Task<IReadOnlyList<T>> GetAsync<T>(
        string endpoint,
        int sessionKey,
        JsonTypeInfo<List<T>> typeInfo,
        CancellationToken cancellationToken)
    {
        var rows = await client.GetAsync(endpoint, sessionKey, typeInfo, cancellationToken).ConfigureAwait(false);
        LogDownloaded(logger, endpoint, sessionKey, rows.Count);
        return rows;
    }

    private async Task<List<Stint>> GetStintsAsync(int sessionKey, CancellationToken cancellationToken)
    {
        var stints = new List<Stint>();
        foreach (var row in await GetAsync("stints", sessionKey, Json.ListOpenF1Stint, cancellationToken).ConfigureAwait(false))
        {
            if (row.ToEntity(sessionKey) is { } stint)
            {
                stints.Add(stint);
            }
            else
            {
                LogUnknownCompound(logger, row.DriverNumber, row.StintNumber, row.Compound);
            }
        }

        return stints;
    }

    // OpenF1 publica starting_grid en la sesión de clasificación (404 con la key de carrera); se guarda con la key de carrera.
    private async Task<List<StartingGrid>> GetStartingGridAsync(int sessionKey, int? qualiSessionKey, CancellationToken cancellationToken)
    {
        if (qualiSessionKey is not { } quali)
        {
            return [];
        }

        return [.. (await GetAsync("starting_grid", quali, Json.ListOpenF1StartingGrid, cancellationToken).ConfigureAwait(false))
            .Select(row => row.ToEntity(sessionKey))];
    }

    private async Task<(List<TeamRadio> Rows, int Downloaded, int Missing)> DownloadTeamRadioAsync(
        int sessionKey,
        IReadOnlyList<OpenF1TeamRadio> rows,
        string radioPath,
        CancellationToken cancellationToken)
    {
        var sessionFolder = sessionKey.ToString(CultureInfo.InvariantCulture);
        var directory = Path.Combine(radioPath, sessionFolder);
        Directory.CreateDirectory(directory);

        var entities = new List<TeamRadio>();
        var downloaded = 0;
        var missing = 0;
        foreach (var row in rows)
        {
            if (!Uri.TryCreate(row.RecordingUrl, UriKind.Absolute, out var recordingUri))
            {
                LogInvalidRecordingUrl(logger, row.DriverNumber, row.Date, row.RecordingUrl);
                continue;
            }

            var fileName = Path.GetFileName(Uri.UnescapeDataString(recordingUri.AbsolutePath));
            var filePath = Path.Combine(directory, fileName);
            if (!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
            {
                var bytes = await client.DownloadAsync(recordingUri, cancellationToken).ConfigureAwait(false);
                if (bytes is null)
                {
                    missing++;
                    LogMissingRecording(logger, recordingUri);
                }
                else
                {
                    await File.WriteAllBytesAsync(filePath, bytes, cancellationToken).ConfigureAwait(false);
                    downloaded++;
                }
            }

            // Ruta relativa a la carpeta raíz de radio, para que la app la resuelva con su propia configuración.
            entities.Add(row.ToEntity(sessionKey, row.RecordingUrl!, $"{sessionFolder}/{fileName}"));
        }

        return (Dedupe(entities, entity => (entity.DriverNumber, entity.Date)), downloaded, missing);
    }

    // Upsert por clave natural: ante filas repetidas gana la última que publicó OpenF1.
    private static List<T> Dedupe<T, TKey>(IEnumerable<T> rows, Func<T, TKey> naturalKey)
        where TKey : notnull =>
        [.. rows.GroupBy(naturalKey).Select(group => group.Last())];

    private static async Task ReplaceAsync<T>(PitWallDbContext context, IQueryable<T> existing, IReadOnlyList<T> rows, CancellationToken cancellationToken)
        where T : class
    {
        await existing.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        foreach (var batch in rows.Chunk(InsertBatchSize))
        {
            context.AddRange(batch);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            context.ChangeTracker.Clear();
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Endpoint} de la sesión {SessionKey}: {Count} filas.")]
    private static partial void LogDownloaded(ILogger logger, string endpoint, int sessionKey, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "car_data y location del piloto {DriverNumber}: {Windows} ventanas.")]
    private static partial void LogTelemetry(ILogger logger, int driverNumber, int windows);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Stint {StintNumber} del piloto {DriverNumber} con compuesto desconocido {Compound}; se omite.")]
    private static partial void LogUnknownCompound(ILogger logger, int driverNumber, int stintNumber, string? compound);

    [LoggerMessage(Level = LogLevel.Warning, Message = "team_radio del piloto {DriverNumber} en {Date} sin recording_url válida ({RecordingUrl}); se omite.")]
    private static partial void LogInvalidRecordingUrl(ILogger logger, int driverNumber, DateTimeOffset date, string? recordingUrl);

    [LoggerMessage(Level = LogLevel.Warning, Message = "El mp3 {RecordingUri} no existe (404); se guarda la fila sin archivo.")]
    private static partial void LogMissingRecording(ILogger logger, Uri recordingUri);
}
