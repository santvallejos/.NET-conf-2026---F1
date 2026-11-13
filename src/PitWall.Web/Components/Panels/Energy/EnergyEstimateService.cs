using System.Data.Common;

using Microsoft.EntityFrameworkCore;

using PitWall.Data;
using PitWall.Strategy.Energy;
using PitWall.Web.Services;

namespace PitWall.Web.Components.Panels.Energy;

/// <summary>
/// Calcula el panel Energía del piloto foco. La telemetría de la sesión se integra una vez al cargarla; cada
/// instante del replay es una búsqueda sobre esa traza, sin mirar muestras posteriores al reloj.
/// </summary>
public sealed class EnergyEstimateService
{
    private readonly IDbContextFactory<PitWallDbContext> _dbContextFactory;
    private readonly EnergyModel _model;
    private readonly ILogger<EnergyEstimateService> _logger;
    private readonly Lock _gate = new();
    private EnergySessionData? _data;

    public EnergyEstimateService(
        IDbContextFactory<PitWallDbContext> dbContextFactory,
        EnergyModel model,
        ILogger<EnergyEstimateService> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(logger);

        _dbContextFactory = dbContextFactory;
        _model = model;
        _logger = logger;
    }

    public bool IsLoaded(int sessionKey)
    {
        lock (_gate)
        {
            return _data?.SessionKey == sessionKey;
        }
    }

    /// <summary>Carga e integra la telemetría de la sesión; si la base no se puede leer, la deja vacía.</summary>
    public async Task LoadAsync(int sessionKey, CancellationToken cancellationToken = default)
    {
        EnergySessionData data;
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            data = await EnergySessionData.LoadAsync(db, _model, sessionKey, cancellationToken);
        }
        catch (DbException exception)
        {
            _logger.LogWarning(exception, "Could not read the telemetry of session {SessionKey} for the energy estimate", sessionKey);
            data = EnergySessionData.Empty(sessionKey);
        }

        lock (_gate)
        {
            _data = data;
        }
    }

    /// <summary>Datos del piloto foco; <c>null</c> si la sesión no está cargada o el piloto no está en carrera.</summary>
    public EnergyPanelData? Get(PitWallView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        EnergySessionData? data;
        lock (_gate)
        {
            data = _data;
        }

        if (view.State is not { } state || data is null || data.SessionKey != state.SessionKey)
        {
            return null;
        }

        var car = state.Cars.FirstOrDefault(candidate => candidate.DriverNumber == view.FocusDriver);
        if (car is null)
        {
            return null;
        }

        var clock = state.Clock;
        var overtakeDisabled = data.IsOvertakeDisabled(clock);
        var overtakeAvailable = EnergyModel.IsOvertakeAvailable(car.Interval, overtakeDisabled);
        var trace = data.Trace(car.DriverNumber);
        if (trace is null)
        {
            return new EnergyPanelData(car.DriverNumber, car.Lap, null, 0, _model.CapacityMj, null, car.Interval, overtakeDisabled, overtakeAvailable);
        }

        // OpenF1 no da inicio de la vuelta 1: se toma la largada de la sesión.
        var raceStart = view.Playback.StartTime ?? DateTimeOffset.MinValue;
        var starts = data.LapStartsUpTo(car.DriverNumber, clock);
        var lapStart = starts.Count > 0 ? starts[^1].Start : raceStart;
        EnergyLapTotals? previous = null;
        if (starts.Count > 0 && starts[^1].Lap > 1)
        {
            var (previousLap, previousStart) = starts.Count > 1 ? starts[^2] : (starts[^1].Lap - 1, raceStart);
            previous = new EnergyLapTotals(
                previousLap,
                trace.HarvestedMj(previousStart, lapStart),
                trace.DeployedMj(previousStart, lapStart));
        }

        return new EnergyPanelData(
            car.DriverNumber,
            car.Lap,
            EnergyModel.Estimate(trace, lapStart, clock, car.Interval, overtakeDisabled),
            trace.SocMjAt(clock),
            trace.CapacityMj,
            previous,
            car.Interval,
            overtakeDisabled,
            overtakeAvailable);
    }
}
