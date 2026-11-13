using PitWall.Domain;

namespace PitWall.Web.Services;

/// <summary>
/// Cambios de un <see cref="RaceState"/> respecto del anterior que se empujan a los clientes. Un snapshot
/// (<see cref="IsSnapshot"/>) trae todos los autos, el clima y la pista; un delta trae solo los autos que
/// cambiaron y <c>null</c> en clima o pista si no cambiaron. El cliente reemplaza cada auto por número.
/// </summary>
public sealed record RaceStateDelta(
    int SessionKey,
    DateTimeOffset Clock,
    int LeaderLap,
    bool IsSnapshot,
    IReadOnlyList<int> Order,
    IReadOnlyList<CarState> Cars,
    WeatherState? Weather,
    TrackStatus? Track,
    IReadOnlyList<RaceControlMessage> NewMessages)
{
    /// <summary>
    /// Delta de <paramref name="current"/> respecto de <paramref name="previous"/>; snapshot si no hay estado
    /// anterior o si cambió la sesión. <see cref="Order"/> (números de piloto por posición) va siempre completo.
    /// </summary>
    public static RaceStateDelta Create(RaceState? previous, RaceState current)
    {
        ArgumentNullException.ThrowIfNull(current);

        IReadOnlyList<int> order = [.. current.Cars.Select(car => car.DriverNumber)];
        if (previous is null || previous.SessionKey != current.SessionKey)
        {
            return new RaceStateDelta(
                current.SessionKey,
                current.Clock,
                current.LeaderLap,
                IsSnapshot: true,
                order,
                current.Cars,
                current.Weather,
                current.Track,
                current.NewMessages);
        }

        var before = previous.Cars.ToDictionary(car => car.DriverNumber);
        IReadOnlyList<CarState> changed =
        [
            .. current.Cars.Where(car => !before.TryGetValue(car.DriverNumber, out var old) || old != car),
        ];

        return new RaceStateDelta(
            current.SessionKey,
            current.Clock,
            current.LeaderLap,
            IsSnapshot: false,
            order,
            changed,
            current.Weather == previous.Weather ? null : current.Weather,
            current.Track == previous.Track ? null : current.Track,
            current.NewMessages);
    }
}
