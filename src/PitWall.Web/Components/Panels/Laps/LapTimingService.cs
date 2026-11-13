using System.Data.Common;

using Microsoft.EntityFrameworkCore;

using PitWall.Data;
using PitWall.Strategy.Timing;
using PitWall.Web.Services;

namespace PitWall.Web.Components.Panels.Laps;

/// <summary>
/// Timing por vuelta (sectores, mejores, vuelta ideal y rankings) al instante del replay. Las vueltas de la sesión
/// se leen de SQLite una vez; cada instante se calcula en memoria y solo cambia cuando se completa alguna vuelta.
/// </summary>
public sealed class LapTimingService
{
    private readonly IDbContextFactory<PitWallDbContext> _dbContextFactory;
    private readonly ILogger<LapTimingService> _logger;
    private readonly Lock _gate = new();
    private int? _sessionKey;
    private SessionLapTiming? _timing;
    private IReadOnlyDictionary<(int DriverNumber, int LapNumber), LapContext> _contexts =
        new Dictionary<(int DriverNumber, int LapNumber), LapContext>();
    private (int SessionKey, int Completed)? _lastKey;
    private LapTimingSnapshot? _last;

    public LapTimingService(IDbContextFactory<PitWallDbContext> dbContextFactory, ILogger<LapTimingService> logger)
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
            return _sessionKey == sessionKey;
        }
    }

    /// <summary>Carga las vueltas de la sesión; si la base no se puede leer, la deja sin vueltas.</summary>
    public async Task LoadAsync(int sessionKey, CancellationToken cancellationToken = default)
    {
        SessionLapTiming timing;
        Dictionary<(int DriverNumber, int LapNumber), LapContext> contexts;
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var (laps, lapContexts) = await LapTimingSessionLaps.LoadAsync(db, sessionKey, cancellationToken);
            timing = new SessionLapTiming(laps);
            contexts = lapContexts;
        }
        catch (DbException exception)
        {
            _logger.LogWarning(exception, "Could not read the laps of session {SessionKey} for lap timing", sessionKey);
            timing = new SessionLapTiming([]);
            contexts = [];
        }

        lock (_gate)
        {
            _sessionKey = sessionKey;
            _timing = timing;
            _contexts = contexts;
            _lastKey = null;
            _last = null;
        }
    }

    /// <summary>Compuesto y neutralización de una vuelta; <c>null</c> si la sesión no está cargada o no hay dato.</summary>
    public LapContext? Context(int sessionKey, int driverNumber, int lapNumber)
    {
        lock (_gate)
        {
            return _sessionKey == sessionKey ? _contexts.GetValueOrDefault((driverNumber, lapNumber)) : null;
        }
    }

    /// <summary>Timing al instante actual del replay; <c>null</c> si la sesión no está cargada.</summary>
    public LapTimingSnapshot? Get(PitWallView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        return view.State is { } state ? Get(state.SessionKey, state.Clock) : null;
    }

    /// <summary>Timing con las vueltas completas a <paramref name="clock"/>; <c>null</c> si la sesión no está cargada.</summary>
    public LapTimingSnapshot? Get(int sessionKey, DateTimeOffset clock)
    {
        SessionLapTiming? timing;
        lock (_gate)
        {
            if (_sessionKey != sessionKey || _timing is null)
            {
                return null;
            }

            timing = _timing;
        }

        var key = (sessionKey, timing.CompletedCount(clock));
        lock (_gate)
        {
            if (_lastKey == key && _last is not null && ReferenceEquals(_timing, timing))
            {
                return _last;
            }
        }

        var snapshot = timing.At(clock);
        lock (_gate)
        {
            if (ReferenceEquals(_timing, timing))
            {
                _lastKey = key;
                _last = snapshot;
            }
        }

        return snapshot;
    }
}
