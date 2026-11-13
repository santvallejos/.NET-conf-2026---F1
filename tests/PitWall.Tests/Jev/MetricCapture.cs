using System.Diagnostics.Metrics;

namespace PitWall.Tests.Jev;

/// <summary>Registra las mediciones de los meters de una <see cref="TestMeterFactory"/>.</summary>
internal sealed class MetricCapture : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly Lock _lock = new();
    private readonly List<MetricMeasurement> _measurements = [];

    public MetricCapture(TestMeterFactory meterFactory)
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (meterFactory.Meters.Contains(instrument.Meter))
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Add(instrument, value, tags));
        _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Add(instrument, value, tags));
        _listener.Start();
    }

    public IReadOnlyList<MetricMeasurement> For(string instrumentName)
    {
        lock (_lock)
        {
            return _measurements.Where(m => m.Instrument == instrumentName).ToList();
        }
    }

    public void Dispose() => _listener.Dispose();

    private void Add(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var tagMap = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            tagMap[tag.Key] = tag.Value?.ToString();
        }

        lock (_lock)
        {
            _measurements.Add(new MetricMeasurement(instrument.Name, value, tagMap));
        }
    }
}
