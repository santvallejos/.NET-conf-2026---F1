using System.Diagnostics.Metrics;

namespace PitWall.Web.Services;

/// <summary>Métricas del cálculo de estrategia para el dashboard de Aspire (docs/architecture.md §5).</summary>
public sealed class StrategyMetrics
{
    public const string MeterName = "PitWall.Strategy";

    private readonly Histogram<double> _monteCarloDuration;

    public StrategyMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(MeterName);
        _monteCarloDuration = meter.CreateHistogram<double>(
            "pitwall.montecarlo.duration",
            unit: "ms",
            description: "Duración de Monte Carlo de un piloto en una vuelta (todas las candidatas).");
    }

    public void RecordMonteCarlo(TimeSpan duration, int driverNumber) =>
        _monteCarloDuration.Record(duration.TotalMilliseconds, new KeyValuePair<string, object?>("driver", driverNumber));
}
