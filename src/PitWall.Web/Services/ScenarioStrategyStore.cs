using PitWall.Strategy.Scenarios;

namespace PitWall.Web.Services;

/// <summary>
/// Estrategia hipotética del escenario activo por piloto y los efectos vigentes de sus eventos. Separada de
/// <see cref="StrategyStore"/>: los paneles de la carrera real nunca ven resultados hipotéticos (ADR-010). La
/// escribe <see cref="StrategyWorker"/>.
/// </summary>
public sealed class ScenarioStrategyStore
{
    private readonly Lock _gate = new();
    private Dictionary<int, ScenarioPublication> _results = [];
    private int? _sessionKey;
    private Guid? _scenarioId;
    private ScenarioEffects _effects = ScenarioEffects.None;
    private Dictionary<Guid, int> _decisionEvents = [];
    private int? _linksSessionKey;

    /// <summary>Se dispara (desde el hilo del worker) cada vez que cambian los efectos o algún resultado.</summary>
    public event Action? Changed;

    public ScenarioPublication? Get(int sessionKey, Guid scenarioId, int driverNumber)
    {
        lock (_gate)
        {
            return _sessionKey == sessionKey && _scenarioId == scenarioId ? _results.GetValueOrDefault(driverNumber) : null;
        }
    }

    /// <summary>Efectos de los eventos del escenario en la vuelta actual; <see cref="ScenarioEffects.None"/> si no es el calculado.</summary>
    public ScenarioEffects Effects(int sessionKey, Guid scenarioId)
    {
        lock (_gate)
        {
            return _sessionKey == sessionKey && _scenarioId == scenarioId ? _effects : ScenarioEffects.None;
        }
    }

    /// <summary>Fija los efectos vigentes; al cambiar de sesión o de escenario descarta los resultados.</summary>
    /// <param name="scenarioId">null = carrera real (sin resultados hipotéticos).</param>
    public void SetEffects(int sessionKey, Guid? scenarioId, ScenarioEffects effects)
    {
        ArgumentNullException.ThrowIfNull(effects);

        lock (_gate)
        {
            if (_sessionKey != sessionKey || _scenarioId != scenarioId || effects.ActiveEvents.Count == 0)
            {
                _results = [];
            }

            _sessionKey = sessionKey;
            _scenarioId = scenarioId;
            _effects = effects;
        }

        Changed?.Invoke();
    }

    /// <summary>Asocia una decisión de Jev al evento inyectado que la disparó (timeline del escenario).</summary>
    public void LinkDecision(int sessionKey, Guid decisionId, int eventId)
    {
        lock (_gate)
        {
            if (_linksSessionKey != sessionKey)
            {
                _linksSessionKey = sessionKey;
                _decisionEvents = [];
            }

            _decisionEvents[decisionId] = eventId;
        }
    }

    /// <summary>Evento inyectado que disparó la decisión, o null.</summary>
    public int? EventOf(int sessionKey, Guid decisionId)
    {
        lock (_gate)
        {
            return _linksSessionKey == sessionKey && _decisionEvents.TryGetValue(decisionId, out var eventId) ? eventId : null;
        }
    }

    public void Publish(ScenarioPublication publication)
    {
        ArgumentNullException.ThrowIfNull(publication);

        lock (_gate)
        {
            if (_sessionKey != publication.SessionKey || _scenarioId != publication.ScenarioId)
            {
                return;
            }

            _results[publication.Publication.Result.Snapshot.DriverNumber] = publication;
        }

        Changed?.Invoke();
    }
}
