using Microsoft.Extensions.Options;

namespace PitWall.Replay;

/// <summary>
/// Reloj de carrera virtual: avanza según la velocidad elegida sobre el tiempo transcurrido de
/// <see cref="TimeProvider"/>, sin depender del reloj real. Saltar de vuelta desde la UI va por
/// <see cref="ReplayEngine.JumpToLap"/>, que además reinicia la ventana de mensajes nuevos.
/// </summary>
public sealed class ReplayClock
{
    private readonly TimeProvider _timeProvider;
    private readonly Lock _gate = new();
    private ReplaySession? _session;
    private DateTimeOffset _anchorClock;
    private long _anchorTimestamp;
    private int _speed;
    private bool _isPlaying;

    public ReplayClock(TimeProvider timeProvider, IOptions<ReplayOptions> options)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);

        _timeProvider = timeProvider;
        Speeds = options.Value.GetSpeeds();
        _speed = Speeds[0];
    }

    public IReadOnlyList<int> Speeds { get; }

    public ReplaySession? Session
    {
        get
        {
            lock (_gate)
            {
                return _session;
            }
        }
    }

    public int Speed
    {
        get
        {
            lock (_gate)
            {
                return _speed;
            }
        }
    }

    public bool IsPlaying
    {
        get
        {
            lock (_gate)
            {
                return _isPlaying;
            }
        }
    }

    public DateTimeOffset Now
    {
        get
        {
            lock (_gate)
            {
                return CurrentClock();
            }
        }
    }

    public bool IsAtEnd
    {
        get
        {
            lock (_gate)
            {
                return _session is not null && CurrentClock() >= _session.EndTime;
            }
        }
    }

    /// <summary>Carga la sesión y deja el reloj en pausa al inicio de la carrera.</summary>
    public void Load(ReplaySession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        lock (_gate)
        {
            _session = session;
            _isPlaying = false;
            Anchor(session.StartTime);
        }
    }

    public void Play()
    {
        lock (_gate)
        {
            RequireSession();
            if (_isPlaying)
            {
                return;
            }

            Anchor(_anchorClock);
            _isPlaying = true;
        }
    }

    public void Pause()
    {
        lock (_gate)
        {
            if (!_isPlaying)
            {
                return;
            }

            Anchor(CurrentClock());
            _isPlaying = false;
        }
    }

    public void SetSpeed(int speed)
    {
        if (!Speeds.Contains(speed))
        {
            throw new ArgumentOutOfRangeException(nameof(speed), speed, $"Replay speed must be one of: {string.Join(", ", Speeds)}.");
        }

        lock (_gate)
        {
            Anchor(CurrentClock());
            _speed = speed;
        }
    }

    /// <summary>Reposiciona el reloj (acotado a la sesión) sin cambiar play/pausa ni velocidad.</summary>
    public DateTimeOffset Seek(DateTimeOffset clock)
    {
        lock (_gate)
        {
            var session = RequireSession();
            var target = clock < session.StartTime ? session.StartTime : clock > session.EndTime ? session.EndTime : clock;
            Anchor(target);
            return target;
        }
    }

    /// <summary>Reposiciona el reloj al inicio de la vuelta <paramref name="lap"/> del líder.</summary>
    public DateTimeOffset JumpToLap(int lap)
    {
        lock (_gate)
        {
            return Seek(RequireSession().GetLapStartTime(lap));
        }
    }

    private void Anchor(DateTimeOffset clock)
    {
        _anchorClock = clock;
        _anchorTimestamp = _timeProvider.GetTimestamp();
    }

    private DateTimeOffset CurrentClock()
    {
        if (!_isPlaying || _session is null)
        {
            return _anchorClock;
        }

        var clock = _anchorClock + (_timeProvider.GetElapsedTime(_anchorTimestamp) * _speed);
        return clock > _session.EndTime ? _session.EndTime : clock;
    }

    private ReplaySession RequireSession() =>
        _session ?? throw new InvalidOperationException("No replay session is loaded.");
}
