using System.Data.Common;

using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using PitWall.Data;
using PitWall.Domain;
using PitWall.Replay;
using PitWall.Web.Hubs;

namespace PitWall.Web.Services;

/// <summary>
/// Sesión de replay activa, compartida por todos los paneles y por los clientes de <see cref="PitWallHub"/>.
/// Recibe cada <see cref="RaceState"/> del replay, rearma <see cref="View"/>, avisa a los paneles con
/// <see cref="Changed"/> y empuja el <see cref="RaceStateDelta"/> por el hub. Los controles (cargar sesión,
/// play/pausa, velocidad, saltar a vuelta, piloto foco) pasan por acá tanto desde la UI como desde el hub.
/// </summary>
public sealed class PitWallSession
{
    public const int MaxRaceControlMessages = 100;

    private const string NoDatabaseError =
        "No se pudo leer la base SQLite de PitWall. Corré la ingesta (PitWall.Ingestion) y reiniciá la app.";

    private readonly ReplayEngine _engine;
    private readonly IDbContextFactory<PitWallDbContext> _dbContextFactory;
    private readonly IHubContext<PitWallHub, IPitWallClient> _hub;
    private readonly ILogger<PitWallSession> _logger;
    private readonly int? _defaultSessionKey;
    private readonly Lock _gate = new();
    private int _focusDriver;
    private IReadOnlyList<SessionInfo> _sessions = [];
    private SessionDetails? _details;
    private RaceState? _state;
    private PlaybackState? _lastPlayback;
    private string? _error;
    private PitWallView _view;

    public PitWallSession(
        ReplayEngine engine,
        IDbContextFactory<PitWallDbContext> dbContextFactory,
        IHubContext<PitWallHub, IPitWallClient> hub,
        IOptions<PitWallOptions> options,
        ILogger<PitWallSession> logger)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        ArgumentNullException.ThrowIfNull(hub);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _engine = engine;
        _dbContextFactory = dbContextFactory;
        _hub = hub;
        _logger = logger;
        _defaultSessionKey = options.Value.DefaultSessionKey;
        _focusDriver = options.Value.FocusDriver;
        TrackedDrivers = [.. new[] { options.Value.FocusDriver }.Concat(options.Value.BackgroundDrivers).Distinct()];
        _view = BuildView();
    }

    /// <summary>Se dispara (desde cualquier hilo) cada vez que cambia <see cref="View"/>.</summary>
    public event Action? Changed;

    public PitWallView View
    {
        get
        {
            lock (_gate)
            {
                return _view;
            }
        }
    }

    /// <summary>
    /// Pilotos elegibles como foco (<c>PitWall:FocusDriver</c> + <c>PitWall:BackgroundDrivers</c>): su estrategia ya se
    /// calcula en segundo plano, así que cambiar de foco no recalcula nada (ADR-006).
    /// </summary>
    public IReadOnlyList<int> TrackedDrivers { get; }

    /// <summary>Lista las sesiones ingeridas y carga la configurada en <c>PitWall:DefaultSessionKey</c> (o la primera).</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var sessions = await RefreshSessionsAsync(cancellationToken);
        if (sessions.Count == 0)
        {
            return;
        }

        var sessionKey = sessions.Any(session => session.SessionKey == _defaultSessionKey)
            ? _defaultSessionKey!.Value
            : sessions[0].SessionKey;
        await LoadSessionAsync(sessionKey, cancellationToken);
    }

    public async Task<IReadOnlyList<SessionInfo>> RefreshSessionsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SessionInfo> sessions;
        string? error = null;
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var rows = await db.IngestedSessions.AsNoTracking().ToListAsync(cancellationToken);
            sessions = [.. rows
                .OrderBy(e => e.DateStart)
                .Select(e => new SessionInfo(e.SessionKey, e.Name, e.Circuit, e.DateStart))];
            if (sessions.Count == 0)
            {
                error = "La base no tiene sesiones ingeridas. Corré la ingesta (PitWall.Ingestion).";
            }
        }
        catch (DbException exception)
        {
            _logger.LogWarning(exception, "Could not read the ingested sessions from SQLite");
            sessions = [];
            error = NoDatabaseError;
        }

        lock (_gate)
        {
            _sessions = sessions;
            _error = error;
        }

        RaiseChanged();
        return sessions;
    }

    /// <summary>Carga la sesión en pausa al inicio de la carrera; si falla, la deja en <see cref="PitWallView.Error"/>.</summary>
    public async Task LoadSessionAsync(int sessionKey, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var details = await SessionDetails.LoadAsync(db, sessionKey, cancellationToken);
            await _engine.LoadSessionAsync(sessionKey, cancellationToken);
            lock (_gate)
            {
                _details = details;
                _error = null;
            }

            _logger.LogInformation("Replay session {SessionKey} loaded", sessionKey);
        }
        catch (Exception exception) when (exception is DbException or ReplaySessionException)
        {
            _logger.LogWarning(exception, "Could not load replay session {SessionKey}", sessionKey);
            lock (_gate)
            {
                _error = exception is ReplaySessionException
                    ? $"La sesión {sessionKey} no tiene datos suficientes para reproducirse."
                    : NoDatabaseError;
            }
        }

        RaiseChanged();
        await NotifyPlaybackIfChangedAsync();
    }

    public Task PlayAsync() => ControlAsync(_engine.Play);

    public Task PauseAsync() => ControlAsync(_engine.Pause);

    /// <exception cref="ArgumentOutOfRangeException">Velocidad fuera de <c>Replay:Speeds</c>.</exception>
    public Task SetSpeedAsync(int speed)
    {
        if (!_engine.Clock.Speeds.Contains(speed))
        {
            throw new ArgumentOutOfRangeException(nameof(speed), speed, $"Replay speed must be one of: {string.Join(", ", _engine.Clock.Speeds)}.");
        }

        return ControlAsync(() => _engine.SetSpeed(speed));
    }

    /// <summary>Salta al inicio de la vuelta <paramref name="lap"/>; el estado nuevo llega por el replay.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Vuelta fuera de 1..<see cref="PlaybackState.LapCount"/>.</exception>
    public Task JumpToLapAsync(int lap)
    {
        var lapCount = _engine.Clock.Session?.LapCount ?? 0;
        if (lapCount > 0)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(lap, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(lap, lapCount);
        }

        return ControlAsync(() => _engine.JumpToLap(lap));
    }

    /// <summary>Cambia el piloto foco de todos los paneles; solo rearma <see cref="View"/> (ADR-006).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Piloto fuera de <see cref="TrackedDrivers"/>.</exception>
    public async Task SetFocusDriverAsync(int driverNumber)
    {
        if (!TrackedDrivers.Contains(driverNumber))
        {
            throw new ArgumentOutOfRangeException(nameof(driverNumber), driverNumber, $"Focus driver must be one of: {string.Join(", ", TrackedDrivers)}.");
        }

        lock (_gate)
        {
            if (_focusDriver == driverNumber)
            {
                return;
            }

            _focusDriver = driverNumber;
        }

        RaiseChanged();
        await _hub.Clients.All.OnFocusChanged(driverNumber);
    }

    /// <summary>Aplica un estado publicado por el replay y empuja su delta a los clientes del hub.</summary>
    public async Task PublishAsync(RaceState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        RaceStateDelta delta;
        lock (_gate)
        {
            delta = RaceStateDelta.Create(_state, state);
            _state = state;
        }

        RaiseChanged();
        await _hub.Clients.All.OnRaceStateDelta(delta);
        await NotifyPlaybackIfChangedAsync();
    }

    /// <summary>Snapshot completo del estado actual para un cliente que recién se conecta.</summary>
    public RaceStateDelta? GetSnapshot()
    {
        lock (_gate)
        {
            return _state is null ? null : RaceStateDelta.Create(null, _state);
        }
    }

    // Sin sesión cargada los controles no hacen nada (el replay lanzaría InvalidOperationException).
    private async Task ControlAsync(Action action)
    {
        if (_engine.Clock.Session is null)
        {
            return;
        }

        action();
        await NotifyPlaybackIfChangedAsync();
    }

    // El replay se pausa solo al llegar al final: por eso también se compara tras cada estado publicado.
    private async Task NotifyPlaybackIfChangedAsync()
    {
        var playback = ReadPlayback();
        lock (_gate)
        {
            if (_lastPlayback is not null && SamePlayback(_lastPlayback, playback))
            {
                return;
            }

            _lastPlayback = playback;
        }

        RaiseChanged();
        await _hub.Clients.All.OnPlaybackChanged(playback);
    }

    private void RaiseChanged()
    {
        lock (_gate)
        {
            _view = BuildView();
        }

        Changed?.Invoke();
    }

    // Se llama con _gate tomado (o desde el constructor).
    private PitWallView BuildView()
    {
        var playback = ReadPlayback();
        var details = _details;
        var state = _state;
        if (details is null || state is null || details.SessionKey != state.SessionKey)
        {
            return new PitWallView(
                _sessions,
                playback,
                state,
                details?.Drivers ?? new Dictionary<int, DriverInfo>(),
                new Dictionary<int, int>(),
                [],
                state is null ? FlagBoard.Empty : FlagBoard.Create(state.Track, [], state.Clock),
                [],
                _focusDriver,
                _error);
        }

        var messages = details.RaceControlUpTo(state.Clock);
        return new PitWallView(
            _sessions,
            playback,
            state,
            details.Drivers,
            details.PitStopsUpTo(state.Clock),
            [.. Enumerable.Reverse(messages).Take(MaxRaceControlMessages)],
            FlagBoard.Create(state.Track, messages, state.Clock),
            details.WeatherUpTo(state.Clock),
            _focusDriver,
            _error);
    }

    private PlaybackState ReadPlayback()
    {
        var clock = _engine.Clock;
        var session = clock.Session;
        return new PlaybackState(
            session?.SessionKey,
            clock.IsPlaying,
            clock.Speed,
            clock.Speeds,
            session?.LapCount ?? 0,
            session?.StartTime,
            session?.EndTime);
    }

    private static bool SamePlayback(PlaybackState left, PlaybackState right) =>
        left.SessionKey == right.SessionKey
        && left.IsPlaying == right.IsPlaying
        && left.Speed == right.Speed
        && left.LapCount == right.LapCount
        && left.StartTime == right.StartTime
        && left.EndTime == right.EndTime;
}
