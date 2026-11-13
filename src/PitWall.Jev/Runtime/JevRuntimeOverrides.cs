using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace PitWall.Jev.Runtime;

/// <summary>
/// Cambio en caliente de <c>Jev:Enabled</c>, <c>Jev:Mode</c>, <c>Jev:Laps</c> y <c>Jev:Drivers</c> sin reiniciar
/// (docs/configuration.md §4): override en memoria aplicado sobre la configuración. Cada cambio invalida
/// <see cref="IOptionsMonitor{TOptions}"/>, así que todos los consumidores (incluido el decorador de costo) leen
/// el valor nuevo en su próxima llamada.
/// </summary>
public sealed class JevRuntimeOverrides : IPostConfigureOptions<JevOptions>, IOptionsChangeTokenSource<JevOptions>
{
    private readonly Lock _gate = new();
    private readonly Dictionary<int, bool> _driverToggles = [];
    private bool? _enabled;
    private JevMode? _mode;
    private int[]? _laps;
    private int[]? _drivers;
    private CancellationTokenSource _changed = new();

    public string? Name => Options.DefaultName;

    public void SetEnabled(bool enabled) => Update(() => _enabled = enabled);

    public void SetMode(JevMode mode) => Update(() => _mode = mode);

    public void SetLaps(IEnumerable<int> laps)
    {
        ArgumentNullException.ThrowIfNull(laps);

        var values = laps.Distinct().Order().ToArray();
        foreach (var lap in values)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(lap, 1, nameof(laps));
        }

        Update(() => _laps = values);
    }

    /// <summary>Reemplaza la lista de pilotos y descarta los on/off individuales previos.</summary>
    public void SetDrivers(IEnumerable<int> drivers)
    {
        ArgumentNullException.ThrowIfNull(drivers);

        var values = drivers.Distinct().ToArray();
        Update(() =>
        {
            _drivers = values;
            _driverToggles.Clear();
        });
    }

    /// <summary>Activa o desactiva Jev para un piloto sin tocar al resto.</summary>
    public void SetDriverEnabled(int driverNumber, bool enabled)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(driverNumber, 1);

        Update(() => _driverToggles[driverNumber] = enabled);
    }

    public IChangeToken GetChangeToken()
    {
        lock (_gate)
        {
            return new CancellationChangeToken(_changed.Token);
        }
    }

    public void PostConfigure(string? name, JevOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (name != Options.DefaultName)
        {
            return;
        }

        lock (_gate)
        {
            options.Enabled = _enabled ?? options.Enabled;
            options.Mode = _mode ?? options.Mode;
            options.Laps = _laps is null ? options.Laps : [.. _laps];

            var drivers = (_drivers ?? options.Drivers).ToList();
            foreach (var (driverNumber, enabled) in _driverToggles)
            {
                drivers.Remove(driverNumber);
                if (enabled)
                {
                    drivers.Add(driverNumber);
                }
            }

            options.Drivers = [.. drivers];
        }
    }

    private void Update(Action apply)
    {
        CancellationTokenSource previous;
        lock (_gate)
        {
            apply();
            previous = _changed;
            _changed = new CancellationTokenSource();
        }

        // Cancelar el token avisa a IOptionsMonitor, que descarta el valor cacheado y lo vuelve a armar. No se
        // libera: un suscriptor puede registrarse todavía sobre el token viejo (ya cancelado).
        previous.Cancel();
    }
}
