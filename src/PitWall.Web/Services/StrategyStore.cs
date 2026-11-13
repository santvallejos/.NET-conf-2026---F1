using PitWall.Domain;
using PitWall.Strategy.Workers;

namespace PitWall.Web.Services;

/// <summary>
/// Última estrategia calculada por piloto en la sesión activa. Los paneles la leen al instante al cambiar de piloto
/// (docs/ui.md §4); la escribe <see cref="StrategyWorker"/>.
/// </summary>
public sealed class StrategyStore
{
    private readonly Lock _gate = new();
    private Dictionary<int, StrategyPublication> _results = [];
    private int? _sessionKey;

    /// <summary>Se dispara (desde el hilo del worker) cada vez que cambia algún resultado.</summary>
    public event Action? Changed;

    /// <summary>Se dispara (desde el hilo del worker) con cada resultado nuevo, después de <see cref="Changed"/>.</summary>
    public event Action<StrategyPublication>? Published;

    /// <summary>Último resultado de <paramref name="driverNumber"/> en <paramref name="sessionKey"/>, o null.</summary>
    public StrategyResult? Get(int sessionKey, int driverNumber)
    {
        lock (_gate)
        {
            return _sessionKey == sessionKey ? _results.GetValueOrDefault(driverNumber)?.Result : null;
        }
    }

    /// <summary>Último resultado de <paramref name="driverNumber"/> con su estado de carrera, o null.</summary>
    public StrategyPublication? GetPublication(int sessionKey, int driverNumber)
    {
        lock (_gate)
        {
            return _sessionKey == sessionKey ? _results.GetValueOrDefault(driverNumber) : null;
        }
    }

    /// <param name="state">Estado de carrera con el que se calculó <paramref name="result"/>.</param>
    public void Publish(RaceState state, StrategyResult result)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(result);

        var publication = new StrategyPublication(state, result);
        lock (_gate)
        {
            if (_sessionKey != state.SessionKey)
            {
                _sessionKey = state.SessionKey;
                _results = [];
            }

            _results[result.Snapshot.DriverNumber] = publication;
        }

        Changed?.Invoke();
        Published?.Invoke(publication);
    }

    /// <summary>Descarta los resultados al cambiar de sesión o saltar en el replay.</summary>
    public void Reset(int sessionKey)
    {
        lock (_gate)
        {
            _sessionKey = sessionKey;
            _results = [];
        }

        Changed?.Invoke();
    }
}
