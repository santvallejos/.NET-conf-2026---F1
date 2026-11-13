using System.Data.Common;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using PitWall.Data;
using PitWall.Domain;
using PitWall.Strategy.Degradation;
using PitWall.Web.Services;

namespace PitWall.Web.Components.Panels.Tyres;

/// <summary>
/// Calcula el panel Neumáticos del piloto foco a partir de las vueltas cerradas hasta el instante del replay.
/// Las vueltas de la sesión se cargan una vez; el modelo se recalcula solo cuando se cierra alguna vuelta.
/// </summary>
public sealed class TyreDegradationService
{
    private readonly IDbContextFactory<PitWallDbContext> _dbContextFactory;
    private readonly TyreDegradationModel _model;
    private readonly DegradationOptions _options;
    private readonly ILogger<TyreDegradationService> _logger;
    private readonly Lock _gate = new();
    private TyreSessionLaps? _laps;
    private (int SessionKey, int DriverNumber, int Completed, int Lap, Compound Compound, int TyreAge)? _lastKey;
    private TyrePanelData? _last;

    public TyreDegradationService(
        IDbContextFactory<PitWallDbContext> dbContextFactory,
        TyreDegradationModel model,
        IOptions<DegradationOptions> options,
        ILogger<TyreDegradationService> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _dbContextFactory = dbContextFactory;
        _model = model;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsLoaded(int sessionKey)
    {
        lock (_gate)
        {
            return _laps?.SessionKey == sessionKey;
        }
    }

    /// <summary>Carga las vueltas de la sesión; si la base no se puede leer, la deja sin vueltas.</summary>
    public async Task LoadAsync(int sessionKey, CancellationToken cancellationToken = default)
    {
        TyreSessionLaps laps;
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            laps = await TyreSessionLaps.LoadAsync(db, sessionKey, cancellationToken);
        }
        catch (DbException exception)
        {
            _logger.LogWarning(exception, "Could not read the laps of session {SessionKey} for tyre degradation", sessionKey);
            laps = TyreSessionLaps.Empty(sessionKey);
        }

        lock (_gate)
        {
            _laps = laps;
            _lastKey = null;
            _last = null;
        }
    }

    /// <summary>Datos del piloto foco; <c>null</c> si la sesión no está cargada o el piloto no está en carrera.</summary>
    public TyrePanelData? Get(PitWallView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        TyreSessionLaps? laps;
        lock (_gate)
        {
            laps = _laps;
        }

        if (view.State is not { } state || laps is null || laps.SessionKey != state.SessionKey || laps.LapCount == 0)
        {
            return null;
        }

        var car = state.Cars.FirstOrDefault(candidate => candidate.DriverNumber == view.FocusDriver);
        if (car is null)
        {
            return null;
        }

        var completed = laps.CompletedCount(state.Clock);
        var key = (state.SessionKey, car.DriverNumber, completed, car.Lap, car.Compound, car.TyreAge);
        lock (_gate)
        {
            if (_lastKey == key && _last is not null)
            {
                return _last;
            }
        }

        var fit = _model.Fit(laps.Completed(completed), car.DriverNumber, car.Compound, car.TyreAge, laps.LapCount);
        var data = new TyrePanelData(
            car.DriverNumber,
            car.Lap,
            laps.LapCount,
            car.Compound,
            car.TyreAge,
            laps.StintsUpTo(car.DriverNumber, car.Lap),
            fit,
            fit is null ? TyreDegradationModel.NoCliff : _model.CliffTyreAge(fit.Tyre.DegPerLap),
            _options.CliffDeltaSeconds);

        lock (_gate)
        {
            if (ReferenceEquals(_laps, laps))
            {
                _lastKey = key;
                _last = data;
            }
        }

        return data;
    }
}
