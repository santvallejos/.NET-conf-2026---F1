using System.Diagnostics.CodeAnalysis;

namespace PitWall.Replay;

/// <summary>Serie temporal ordenada por fecha con búsqueda binaria; orden estable ante fechas iguales.</summary>
internal sealed class Timeline<T>
{
    private readonly DateTimeOffset[] _dates;
    private readonly T[] _values;

    public Timeline(IEnumerable<(DateTimeOffset Date, T Value)> points)
    {
        var ordered = points.OrderBy(point => point.Date).ToArray();
        _dates = [.. ordered.Select(point => point.Date)];
        _values = [.. ordered.Select(point => point.Value)];
    }

    public int Count => _values.Length;

    public T this[int index] => _values[index];

    public DateTimeOffset DateAt(int index) => _dates[index];

    public bool TryGetAtOrBefore(DateTimeOffset date, [MaybeNullWhen(false)] out T value)
    {
        var index = UpperBound(date) - 1;
        if (index < 0)
        {
            value = default;
            return false;
        }

        value = _values[index];
        return true;
    }

    /// <summary>Valores con fecha en el intervalo (<paramref name="after"/>, <paramref name="upTo"/>].</summary>
    public IReadOnlyList<T> Between(DateTimeOffset after, DateTimeOffset upTo)
    {
        var start = UpperBound(after);
        var end = UpperBound(upTo);
        return end <= start ? [] : _values[start..end];
    }

    private int UpperBound(DateTimeOffset date)
    {
        var low = 0;
        var high = _dates.Length;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (_dates[middle] <= date)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }
}
