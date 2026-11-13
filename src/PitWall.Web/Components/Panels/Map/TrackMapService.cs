using System.Data.Common;

using Microsoft.EntityFrameworkCore;

using PitWall.Data;
using PitWall.Web.Services;

namespace PitWall.Web.Components.Panels.Map;

/// <summary>
/// Calcula el panel Mapa: trazado del circuito y posición de cada auto con <c>location</c> ingerida (los pilotos
/// seguidos; el resto de la parrilla no tiene posición en pista, ADR-006). Sin mirar muestras posteriores al reloj.
/// </summary>
public sealed class TrackMapService
{
    // Más vieja que esto, la posición ya no representa al auto (abandono, garaje o hueco en los datos).
    private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(10);

    private readonly IDbContextFactory<PitWallDbContext> _dbContextFactory;
    private readonly ILogger<TrackMapService> _logger;
    private readonly Lock _gate = new();
    private TrackMapSessionData? _data;

    public TrackMapService(IDbContextFactory<PitWallDbContext> dbContextFactory, ILogger<TrackMapService> logger)
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

    /// <summary>Lee las posiciones de la sesión y arma el trazado; si la base no se puede leer, lo deja vacío.</summary>
    public async Task LoadAsync(int sessionKey, CancellationToken cancellationToken = default)
    {
        TrackMapSessionData data;
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            data = await TrackMapSessionData.LoadAsync(db, sessionKey, cancellationToken);
        }
        catch (DbException exception)
        {
            _logger.LogWarning(exception, "Could not read the locations of session {SessionKey}", sessionKey);
            data = TrackMapSessionData.Empty(sessionKey);
        }

        lock (_gate)
        {
            _data = data;
        }
    }

    /// <summary>Mapa en el instante actual; <c>null</c> si la sesión no está cargada o no tiene <c>location</c>.</summary>
    public TrackMapData? Get(PitWallView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        TrackMapSessionData? data;
        lock (_gate)
        {
            data = _data;
        }

        if (view.State is not { } state || data is null || data.SessionKey != state.SessionKey || data.IsEmpty)
        {
            return null;
        }

        return new TrackMapData(data.Width, data.Height, data.Outline, data.StartFinish, data.CarsAt(state.Clock, StaleAfter));
    }
}
