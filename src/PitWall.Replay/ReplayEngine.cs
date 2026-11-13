using System.Threading.Channels;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using PitWall.Data;
using PitWall.Domain;

namespace PitWall.Replay;

/// <summary>
/// Worker del replay: en cada tick (<see cref="ReplayOptions.TickMs"/>) arma el <see cref="RaceState"/> del instante
/// del <see cref="ReplayClock"/> y lo publica a cada suscriptor por su propio canal.
/// </summary>
public sealed class ReplayEngine : BackgroundService
{
    // Un suscriptor lento pierde los estados más viejos, nunca frena al replay ni a los demás suscriptores.
    private const int SubscriberCapacity = 64;

    private readonly ReplayClock _clock;
    private readonly IDbContextFactory<PitWallDbContext> _dbContextFactory;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _tickInterval;
    private readonly ILogger<ReplayEngine> _logger;
    private readonly Lock _gate = new();
    private readonly List<Channel<RaceState>> _subscribers = [];
    private RaceStateBuilder? _builder;
    private DateTimeOffset _lastClock;
    private RaceState? _current;
    private PeriodicTimer? _timer;

    public ReplayEngine(
        ReplayClock clock,
        IDbContextFactory<PitWallDbContext> dbContextFactory,
        TimeProvider timeProvider,
        IOptions<ReplayOptions> options,
        ILogger<ReplayEngine> logger)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _clock = clock;
        _dbContextFactory = dbContextFactory;
        _timeProvider = timeProvider;
        _tickInterval = TimeSpan.FromMilliseconds(options.Value.TickMs);
        _logger = logger;
    }

    public ReplayClock Clock => _clock;

    /// <summary>Último estado publicado; <c>null</c> si no hay sesión cargada.</summary>
    public RaceState? Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    /// <summary>Nuevo suscriptor; si ya hay estado publicado lo recibe de inmediato.</summary>
    public ReplaySubscription Subscribe()
    {
        var channel = Channel.CreateBounded<RaceState>(new BoundedChannelOptions(SubscriberCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true,
        });

        lock (_gate)
        {
            _subscribers.Add(channel);
            if (_current is not null)
            {
                channel.Writer.TryWrite(_current);
            }
        }

        return new ReplaySubscription(channel.Reader, () => Unsubscribe(channel));
    }

    /// <summary>Lee la sesión de SQLite una sola vez, la cachea y la carga en pausa al inicio de la carrera.</summary>
    public async Task<RaceState> LoadSessionAsync(int sessionKey, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var session = await ReplaySession.LoadAsync(db, sessionKey, cancellationToken);
        return Load(session);
    }

    public RaceState Load(ReplaySession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        lock (_gate)
        {
            _clock.Load(session);
            _builder = new RaceStateBuilder(session);
            return Publish(resetMessages: true);
        }
    }

    public void Play() => _clock.Play();

    public void Pause() => _clock.Pause();

    public void SetSpeed(int speed) => _clock.SetSpeed(speed);

    /// <summary>
    /// Salta al inicio de la vuelta <paramref name="lap"/> y publica el estado. Los mensajes de Race Control
    /// anteriores al salto no se reportan como nuevos.
    /// </summary>
    public RaceState JumpToLap(int lap)
    {
        lock (_gate)
        {
            RequireBuilder();
            _clock.JumpToLap(lap);
            return Publish(resetMessages: true);
        }
    }

    /// <summary>Publica el estado del instante actual si el reloj está en play; al llegar al final, pausa.</summary>
    public RaceState? Tick()
    {
        lock (_gate)
        {
            if (_builder is null || !_clock.IsPlaying)
            {
                return null;
            }

            var state = Publish(resetMessages: false);
            if (_clock.IsAtEnd)
            {
                _clock.Pause();
            }

            return state;
        }
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        // El timer se crea antes de que ExecuteAsync corra en segundo plano, para que un FakeTimeProvider
        // avanzado justo después del arranque ya lo dispare.
        _timer = new PeriodicTimer(_tickInterval, _timeProvider);
        return base.StartAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _timer?.Dispose();
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timer = _timer ?? throw new InvalidOperationException("The replay timer was not created.");
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    Tick();
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Replay tick failed at {Clock}", _clock.Now);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            CompleteSubscribers();
        }
    }

    private RaceState Publish(bool resetMessages)
    {
        var builder = RequireBuilder();
        var now = _clock.Now;
        var state = builder.Build(now, resetMessages ? now : _lastClock);
        _lastClock = now;
        _current = state;
        foreach (var subscriber in _subscribers)
        {
            subscriber.Writer.TryWrite(state);
        }

        return state;
    }

    private RaceStateBuilder RequireBuilder() =>
        _builder ?? throw new InvalidOperationException("No replay session is loaded.");

    private void Unsubscribe(Channel<RaceState> channel)
    {
        lock (_gate)
        {
            if (_subscribers.Remove(channel))
            {
                channel.Writer.TryComplete();
            }
        }
    }

    private void CompleteSubscribers()
    {
        lock (_gate)
        {
            foreach (var subscriber in _subscribers)
            {
                subscriber.Writer.TryComplete();
            }

            _subscribers.Clear();
        }
    }
}
