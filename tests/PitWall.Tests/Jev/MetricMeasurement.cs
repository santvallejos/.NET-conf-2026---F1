namespace PitWall.Tests.Jev;

internal sealed record MetricMeasurement(string Instrument, double Value, IReadOnlyDictionary<string, string?> Tags);
