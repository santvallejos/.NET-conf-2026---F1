namespace PitWall.Ingestion.OpenF1;

/// <summary>Rango <c>[Start, End)</c> de una descarga de <c>car_data</c>/<c>location</c>.</summary>
public readonly record struct TelemetryWindow(DateTimeOffset Start, DateTimeOffset End)
{
    /// <summary>Parte <c>[start, end)</c> en ventanas consecutivas de <paramref name="size"/>; la última se recorta a <paramref name="end"/>.</summary>
    public static IReadOnlyList<TelemetryWindow> Split(DateTimeOffset start, DateTimeOffset end, TimeSpan size)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(size, TimeSpan.Zero);

        var windows = new List<TelemetryWindow>();
        for (var windowStart = start; windowStart < end; windowStart += size)
        {
            var windowEnd = windowStart + size;
            windows.Add(new TelemetryWindow(windowStart, windowEnd < end ? windowEnd : end));
        }

        return windows;
    }
}
