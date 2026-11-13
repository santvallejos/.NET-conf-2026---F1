using PitWall.Domain;

namespace PitWall.Strategy.Energy;

/// <summary>
/// Energía 2026 ESTIMADA desde <c>car_data</c> (docs/strategy-engine.md §2, ADR-013): OpenF1 no expone batería ni
/// ERS, así que el estado de carga se integra muestra a muestra con potencias fijas de configuración. No es dato
/// real: toda salida lleva <see cref="EnergyEstimate.IsEstimated"/> = <c>true</c>.
/// </summary>
public sealed class EnergyModel
{
    /// <summary>Reglamento 2026: overtake mode a menos de 1 s del auto de adelante.</summary>
    public const double OvertakeIntervalSeconds = 1.0;

    /// <summary>Por debajo de este acelerador (%) y por encima de <see cref="LiftSpeedMin"/> se cuenta lift &amp; coast.</summary>
    public const int LiftThrottleMax = 20;

    public const int LiftSpeedMin = 150;

    /// <summary>Por encima de este acelerador (%) y de <see cref="EnergyOptions.SpeedDeployMin"/> se despliega.</summary>
    public const int DeployThrottleMin = 95;

    /// <summary>
    /// Huecos de telemetría más largos que esto (ventanas de descarga, cortes de OpenF1) no se integran: a ~3,7 Hz
    /// las muestras están a ~0,27 s y extrapolar un estado varios segundos inventaría energía.
    /// </summary>
    public const double MaxSampleGapSeconds = 2.0;

    private readonly EnergyOptions _options;

    public EnergyModel(EnergyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.BatteryCapacityMj);
        ArgumentOutOfRangeException.ThrowIfNegative(options.HarvestBrakeKw);
        ArgumentOutOfRangeException.ThrowIfNegative(options.HarvestLiftKw);
        ArgumentOutOfRangeException.ThrowIfNegative(options.DeployKw);

        _options = options;
    }

    public double CapacityMj => _options.BatteryCapacityMj;

    /// <summary>Overtake mode disponible: a menos de 1 s del de adelante y sin "OVERTAKE DISABLED" vigente.</summary>
    /// <param name="interval">Segundos al auto de adelante; <c>null</c> para el líder o sin dato.</param>
    public static bool IsOvertakeAvailable(double? interval, bool overtakeDisabled) =>
        !overtakeDisabled && interval is >= 0 and < OvertakeIntervalSeconds;

    /// <summary>Regla de la muestra; el freno manda sobre el acelerador si vienen los dos.</summary>
    public EnergyPhase Classify(TelemetrySample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        if (sample.Brake > 0)
        {
            return EnergyPhase.HarvestBrake;
        }

        if (sample.Throttle < LiftThrottleMax && sample.Speed > LiftSpeedMin)
        {
            return EnergyPhase.HarvestLift;
        }

        if (sample.Throttle > DeployThrottleMin && sample.Speed > _options.SpeedDeployMin)
        {
            return EnergyPhase.Deploy;
        }

        return EnergyPhase.None;
    }

    /// <summary>
    /// Integra el SoC sobre <paramref name="samples"/>. Cada muestra mantiene su regla hasta la siguiente
    /// (<c>energía = potencia · Δt</c>) y el SoC se acota en <c>[0, BatteryCapacityMj]</c>: lo que no entra en la
    /// batería no se cuenta como recuperado y no se despliega más de lo que hay.
    /// </summary>
    public EnergyTrace Integrate(IEnumerable<TelemetrySample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        var ordered = samples.OrderBy(sample => sample.Date).ToArray();
        var capacity = _options.BatteryCapacityMj;
        var dates = new DateTimeOffset[ordered.Length];
        var socs = new double[ordered.Length];
        var harvested = new double[ordered.Length];
        var deployed = new double[ordered.Length];

        // ESTIMADO: sin dato de la batería, se asume cargada al empezar la telemetría (sale del garaje a plena carga).
        var soc = capacity;
        var totalHarvested = 0.0;
        var totalDeployed = 0.0;
        for (var i = 0; i < ordered.Length; i++)
        {
            if (i > 0)
            {
                var seconds = (ordered[i].Date - ordered[i - 1].Date).TotalSeconds;
                if (seconds is > 0 and <= MaxSampleGapSeconds)
                {
                    switch (Classify(ordered[i - 1]))
                    {
                        case EnergyPhase.HarvestBrake:
                            totalHarvested += Charge(ref soc, _options.HarvestBrakeKw, seconds);
                            break;
                        case EnergyPhase.HarvestLift:
                            totalHarvested += Charge(ref soc, _options.HarvestLiftKw, seconds);
                            break;
                        case EnergyPhase.Deploy:
                            var drawn = Math.Min(soc, KwToMj(_options.DeployKw, seconds));
                            soc -= drawn;
                            totalDeployed += drawn;
                            break;
                    }
                }
            }

            dates[i] = ordered[i].Date;
            socs[i] = soc;
            harvested[i] = totalHarvested;
            deployed[i] = totalDeployed;
        }

        return new EnergyTrace(capacity, capacity, dates, socs, harvested, deployed);

        double Charge(ref double current, double kw, double seconds)
        {
            var stored = Math.Min(capacity - current, KwToMj(kw, seconds));
            current += stored;
            return stored;
        }
    }

    /// <summary>
    /// Estimación en <paramref name="clock"/>: SoC actual y energía recuperada/desplegada desde
    /// <paramref name="lapStart"/> (inicio de la vuelta que se reporta).
    /// </summary>
    public static EnergyEstimate Estimate(EnergyTrace trace, DateTimeOffset lapStart, DateTimeOffset clock, double? interval, bool overtakeDisabled)
    {
        ArgumentNullException.ThrowIfNull(trace);

        return new EnergyEstimate(
            SocPct: trace.SocMjAt(clock) / trace.CapacityMj * 100.0,
            HarvestedLapMj: trace.HarvestedMj(lapStart, clock),
            DeployedLapMj: trace.DeployedMj(lapStart, clock),
            OvertakeAvailable: IsOvertakeAvailable(interval, overtakeDisabled),
            IsEstimated: true);
    }

    // kW · s = kJ.
    private static double KwToMj(double kw, double seconds) => kw * seconds / 1000.0;
}
