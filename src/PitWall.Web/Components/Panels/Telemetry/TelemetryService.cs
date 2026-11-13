using System.Data.Common;

using Microsoft.EntityFrameworkCore;

using PitWall.Data;
using PitWall.Web.Services;

namespace PitWall.Web.Components.Panels.Telemetry;

/// <summary>
/// Calcula el panel Telemetría del piloto foco. La telemetría de la sesión se lee una vez al cargarla; cada instante
/// del replay es una búsqueda sobre esas muestras, sin mirar muestras posteriores al reloj.
/// </summary>
public sealed class TelemetryService
{
    private const double KmhToMs = 1 / 3.6;

    private readonly IDbContextFactory<PitWallDbContext> _dbContextFactory;
    private readonly ILogger<TelemetryService> _logger;
    private readonly Lock _gate = new();
    private TelemetrySessionData? _data;

    public TelemetryService(IDbContextFactory<PitWallDbContext> dbContextFactory, ILogger<TelemetryService> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public bool IsLoaded(int sessionKey)
    {
        lock (_gate)
        {
            return _data?.SessionKey == sessionKey;
        }
    }

    /// <summary>Lee la telemetría de la sesión; si la base no se puede leer, la deja vacía.</summary>
    public async Task LoadAsync(int sessionKey, CancellationToken cancellationToken = default)
    {
        TelemetrySessionData data;
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            data = await TelemetrySessionData.LoadAsync(db, sessionKey, cancellationToken);
        }
        catch (DbException exception)
        {
            _logger.LogWarning(exception, "Could not read the telemetry of session {SessionKey}", sessionKey);
            data = TelemetrySessionData.Empty(sessionKey);
        }

        lock (_gate)
        {
            _data = data;
        }
    }

    /// <summary>Datos del piloto foco; <c>null</c> si la sesión no está cargada o no hay <c>car_data</c> del piloto.</summary>
    public TelemetryPanelData? Get(PitWallView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        TelemetrySessionData? data;
        lock (_gate)
        {
            data = _data;
        }

        var driverNumber = view.FocusDriver;
        if (view.State is not { } state || data is null || data.SessionKey != state.SessionKey || !data.HasTelemetry(driverNumber))
        {
            return null;
        }

        var clock = state.Clock;
        var laps = data.Laps(driverNumber);

        // OpenF1 puede no dar inicio de la vuelta 1: antes de la primera vuelta conocida se toma la largada.
        var started = laps.LastOrDefault(lap => lap.Start <= clock);
        var (currentLap, currentStart) = started.Lap > 0
            ? (started.Lap, started.Start)
            : (1, view.Playback.StartTime ?? clock);
        var current = new TelemetryLap(currentLap, null, Trace(data.Between(driverNumber, currentStart, clock), currentStart));

        TelemetryLap? best = null;
        var completed = laps
            .Where(lap => lap.Duration is > 0 && lap.Start.AddSeconds(lap.Duration.Value) <= clock)
            .ToList();
        if (completed.Count > 0)
        {
            var (lap, start, duration) = completed.MinBy(lap => lap.Duration!.Value);
            best = new TelemetryLap(lap, duration, Trace(data.Between(driverNumber, start, start.AddSeconds(duration!.Value)), start));
        }

        double? delta = null;
        if (best is not null && current.Points.Count > 0 && best.ElapsedAt(current.DistanceM) is { } bestElapsed)
        {
            delta = current.Points[^1].ElapsedSeconds - bestElapsed;
        }

        return new TelemetryPanelData(driverNumber, data.At(driverNumber, clock), current, best, delta);
    }

    // car_data no trae distancia: se integra la velocidad (trapecios) desde el inicio de la vuelta. Alcanza para
    // alinear dos vueltas del mismo piloto, no es la distancia oficial del circuito.
    private static List<TelemetryPoint> Trace(ArraySegment<TelemetrySample> samples, DateTimeOffset lapStart)
    {
        var points = new List<TelemetryPoint>(samples.Count);
        var distance = 0.0;
        TelemetrySample? previous = null;
        foreach (var sample in samples)
        {
            if (previous is { } before)
            {
                var seconds = (sample.Date - before.Date).TotalSeconds;
                distance += (before.Speed + sample.Speed) / 2.0 * KmhToMs * seconds;
            }

            points.Add(new TelemetryPoint(distance, (sample.Date - lapStart).TotalSeconds, sample));
            previous = sample;
        }

        return points;
    }
}
