using PitWall.Strategy.Workers;

namespace PitWall.Web.Services;

/// <summary>
/// Última estrategia calculada por piloto en la sesión activa. Los paneles la leen al instante al cambiar de piloto
/// (docs/ui.md §4); la escribe <see cref="StrategyWorker"/>.
/// </summary>
public sealed class StrategyStore
{
    private readonly Lock _gate = new();
    private Dictionary<int, StrategyResult> _results = [];
    private int? _sessionKey;

    /// <summary>Se dispara (desde el hilo del worker) cada vez que cambia algún resultado.</summary>
    public event Action? Changed;

    /// <summary>Último resultado de <paramref name="driverNumber"/> en <paramref name="sessionKey"/>, o null.</summary>
    public StrategyResult? Get(int sessionKey, int driverNumber)
    {
        lock (_gate)
        {
            return _sessionKey == sessionKey ? _results.GetValueOrDefault(driverNumber) : null;
        }
    }

    public void Publish(int sessionKey, StrategyResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        lock (_gate)
        {
            if (_sessionKey != sessionKey)
            {
                _sessionKey = sessionKey;
                _results = [];
            }

            _results[result.Snapshot.DriverNumber] = result;
        }

        Changed?.Invoke();
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
