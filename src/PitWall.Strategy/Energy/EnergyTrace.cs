namespace PitWall.Strategy.Energy;

/// <summary>
/// Estado de carga y energía acumulada (recuperada y desplegada) después de cada muestra de telemetría, en orden
/// cronológico. Lo arma <see cref="EnergyModel.Integrate"/>; las consultas por instante son búsquedas binarias.
/// ESTIMADO (ADR-013).
/// </summary>
public sealed class EnergyTrace
{
    private readonly DateTimeOffset[] _dates;
    private readonly double[] _socMj;
    private readonly double[] _harvestedMj;
    private readonly double[] _deployedMj;

    internal EnergyTrace(double capacityMj, double initialSocMj, DateTimeOffset[] dates, double[] socMj, double[] harvestedMj, double[] deployedMj)
    {
        CapacityMj = capacityMj;
        InitialSocMj = initialSocMj;
        _dates = dates;
        _socMj = socMj;
        _harvestedMj = harvestedMj;
        _deployedMj = deployedMj;
    }

    public double CapacityMj { get; }

    /// <summary>SoC antes de la primera muestra.</summary>
    public double InitialSocMj { get; }

    public int Count => _dates.Length;

    /// <summary>Fecha de la última muestra en o antes de <paramref name="instant"/>; <c>null</c> si no hay ninguna.</summary>
    public DateTimeOffset? LastSampleAt(DateTimeOffset instant) => IndexAt(instant) is var index and >= 0 ? _dates[index] : null;

    /// <summary>SoC (MJ) tras la última muestra en o antes de <paramref name="instant"/>.</summary>
    public double SocMjAt(DateTimeOffset instant) => IndexAt(instant) is var index and >= 0 ? _socMj[index] : InitialSocMj;

    /// <summary>Energía recuperada (MJ) entre dos instantes; ya acotada por la capacidad de la batería.</summary>
    public double HarvestedMj(DateTimeOffset from, DateTimeOffset to) => Between(_harvestedMj, from, to);

    /// <summary>Energía desplegada (MJ) entre dos instantes; ya acotada por la carga disponible.</summary>
    public double DeployedMj(DateTimeOffset from, DateTimeOffset to) => Between(_deployedMj, from, to);

    private double Between(double[] cumulative, DateTimeOffset from, DateTimeOffset to)
    {
        if (to <= from)
        {
            return 0;
        }

        var end = IndexAt(to);
        var start = IndexAt(from);
        return (end >= 0 ? cumulative[end] : 0) - (start >= 0 ? cumulative[start] : 0);
    }

    // Índice de la última muestra con fecha <= instant, o -1.
    private int IndexAt(DateTimeOffset instant)
    {
        var low = 0;
        var high = _dates.Length;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (_dates[middle] <= instant)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low - 1;
    }
}
