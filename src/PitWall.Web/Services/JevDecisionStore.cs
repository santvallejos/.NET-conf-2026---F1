using PitWall.Domain;

namespace PitWall.Web.Services;

/// <summary>
/// Decisiones de Jev de la sesión activa en memoria, de la más vieja a la más nueva, para el historial de los
/// paneles; la persistencia completa está en <c>JevDecisionLog</c>. La escribe <see cref="JevWorker"/>.
/// </summary>
public sealed class JevDecisionStore
{
    public const int MaxDecisions = 1000;

    private readonly Lock _gate = new();
    private readonly List<JevDecision> _decisions = [];
    private int? _sessionKey;

    /// <summary>Se dispara (desde el hilo del worker) con cada decisión nueva.</summary>
    public event Action<JevDecision>? Published;

    public IReadOnlyList<JevDecision> Get(int sessionKey)
    {
        lock (_gate)
        {
            return _sessionKey == sessionKey ? [.. _decisions] : [];
        }
    }

    /// <summary>Agrega la decisión; las de una sesión que ya no es la activa se descartan.</summary>
    public void Add(JevDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        lock (_gate)
        {
            if (_sessionKey is { } current && current != decision.SessionKey)
            {
                return;
            }

            _sessionKey = decision.SessionKey;
            _decisions.Add(decision);
            if (_decisions.Count > MaxDecisions)
            {
                _decisions.RemoveRange(0, _decisions.Count - MaxDecisions);
            }
        }

        Published?.Invoke(decision);
    }

    /// <summary>Descarta el historial al cambiar de sesión.</summary>
    public void Reset(int sessionKey)
    {
        lock (_gate)
        {
            _sessionKey = sessionKey;
            _decisions.Clear();
        }
    }
}
