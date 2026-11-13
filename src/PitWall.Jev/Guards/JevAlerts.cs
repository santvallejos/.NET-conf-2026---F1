namespace PitWall.Jev.Guards;

/// <summary>Alertas activas de Jev; una por (tipo, sesión) para no repetirlas en cada request.</summary>
public sealed class JevAlerts
{
    private readonly Lock _lock = new();
    private readonly List<JevAlert> _active = [];

    public event Action<JevAlert>? Raised;

    public IReadOnlyList<JevAlert> Active
    {
        get
        {
            lock (_lock)
            {
                return [.. _active];
            }
        }
    }

    /// <returns>true si la alerta es nueva.</returns>
    public bool Raise(JevAlert alert)
    {
        ArgumentNullException.ThrowIfNull(alert);

        lock (_lock)
        {
            if (_active.Exists(a => a.Kind == alert.Kind && a.SessionKey == alert.SessionKey))
            {
                return false;
            }

            _active.Add(alert);
        }

        Raised?.Invoke(alert);
        return true;
    }

    /// <returns>true si la alerta estaba activa; desde ahí puede volver a emitirse.</returns>
    public bool Resolve(JevAlertKind kind, int sessionKey)
    {
        lock (_lock)
        {
            return _active.RemoveAll(a => a.Kind == kind && a.SessionKey == sessionKey) > 0;
        }
    }
}
