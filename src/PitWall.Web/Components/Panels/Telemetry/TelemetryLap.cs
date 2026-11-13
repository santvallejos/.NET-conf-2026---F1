namespace PitWall.Web.Components.Panels.Telemetry;

/// <summary>Traza de una vuelta; <see cref="LapTime"/> es null si la vuelta está en curso.</summary>
public sealed record TelemetryLap(int Lap, double? LapTime, IReadOnlyList<TelemetryPoint> Points)
{
    public double DistanceM => Points.Count == 0 ? 0 : Points[^1].DistanceM;

    /// <summary>Tiempo de vuelta al pasar por <paramref name="distanceM"/>, interpolado; null fuera de la traza.</summary>
    public double? ElapsedAt(double distanceM)
    {
        if (Points.Count == 0 || distanceM < 0 || distanceM > DistanceM)
        {
            return null;
        }

        var low = 0;
        var high = Points.Count - 1;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (Points[middle].DistanceM < distanceM)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        if (low == 0)
        {
            return Points[0].ElapsedSeconds;
        }

        var before = Points[low - 1];
        var after = Points[low];
        var span = after.DistanceM - before.DistanceM;
        return span <= 0
            ? after.ElapsedSeconds
            : before.ElapsedSeconds + ((distanceM - before.DistanceM) / span * (after.ElapsedSeconds - before.ElapsedSeconds));
    }
}
