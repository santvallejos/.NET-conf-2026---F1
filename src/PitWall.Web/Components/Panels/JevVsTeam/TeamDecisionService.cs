using System.Data.Common;

using Microsoft.EntityFrameworkCore;

using PitWall.Data;
using PitWall.Strategy.TeamDecision;

namespace PitWall.Web.Components.Panels.JevVsTeam;

/// <summary>
/// Decisión real del equipo por vuelta en la sesión activa, derivada de <c>stints</c> + <c>pit</c>. Se lee de SQLite
/// una vez por sesión y se deriva una vez por piloto. Incluye la carrera completa: es un replay de datos conocidos.
/// </summary>
public sealed class TeamDecisionService
{
    private readonly IDbContextFactory<PitWallDbContext> _dbContextFactory;
    private readonly ILogger<TeamDecisionService> _logger;
    private readonly Lock _gate = new();
    private readonly Dictionary<int, IReadOnlyList<TeamLapDecision>> _byDriver = [];
    private int? _sessionKey;
    private TeamStint[] _stints = [];
    private TeamPitStop[] _pitStops = [];

    public TeamDecisionService(IDbContextFactory<PitWallDbContext> dbContextFactory, ILogger<TeamDecisionService> logger)
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

    /// <summary>Carga stints y paradas de la sesión; si la base no se puede leer, la deja sin datos.</summary>
    public async Task LoadAsync(int sessionKey, CancellationToken cancellationToken = default)
    {
        TeamStint[] stints;
        TeamPitStop[] pitStops;
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            stints = await db.Stints.AsNoTracking()
                .Where(e => e.SessionKey == sessionKey)
                .Select(e => new TeamStint(e.DriverNumber, e.StintNumber, e.LapStart, e.LapEnd, e.Compound))
                .ToArrayAsync(cancellationToken);
            pitStops = await db.PitStops.AsNoTracking()
                .Where(e => e.SessionKey == sessionKey)
                .Select(e => new TeamPitStop(e.DriverNumber, e.LapNumber))
                .ToArrayAsync(cancellationToken);
        }
        catch (DbException exception)
        {
            _logger.LogWarning(exception, "Could not read the stints and pit stops of session {SessionKey} for the team decision", sessionKey);
            stints = [];
            pitStops = [];
        }

        lock (_gate)
        {
            _sessionKey = sessionKey;
            _stints = stints;
            _pitStops = pitStops;
            _byDriver.Clear();
        }
    }

    /// <summary>Decisión real del piloto por vuelta; vacía si la sesión no está cargada o no hay datos.</summary>
    public IReadOnlyList<TeamLapDecision> Get(int sessionKey, int driverNumber)
    {
        lock (_gate)
        {
            if (_sessionKey != sessionKey)
            {
                return [];
            }

            if (!_byDriver.TryGetValue(driverNumber, out var decisions))
            {
                decisions = TeamDecisionDeriver.Derive(driverNumber, _stints, _pitStops);
                _byDriver[driverNumber] = decisions;
            }

            return decisions;
        }
    }
}
