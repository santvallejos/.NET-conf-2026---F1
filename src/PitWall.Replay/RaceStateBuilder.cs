using PitWall.Domain;

namespace PitWall.Replay;

/// <summary>Arma el <see cref="RaceState"/> de un instante a partir de los datos cacheados de la sesión.</summary>
public sealed class RaceStateBuilder
{
    private static readonly WeatherState NoWeather = new(0, 0, 0, Rainfall: false);

    private readonly ReplaySession _session;

    public RaceStateBuilder(ReplaySession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
    }

    /// <summary>
    /// Estado en <paramref name="clock"/>. <see cref="RaceState.NewMessages"/> son los mensajes de Race Control con
    /// fecha en (<paramref name="messagesAfter"/>, <paramref name="clock"/>]. Los pilotos sin stints no se incluyen.
    /// </summary>
    public RaceState Build(DateTimeOffset clock, DateTimeOffset messagesAfter)
    {
        var cars = new List<CarState>(_session.Drivers.Count);
        foreach (var driver in _session.Drivers.Where(driver => driver.HasStints))
        {
            var lap = driver.LapAt(clock);
            var (compound, tyreAge) = driver.TyreAt(lap);
            var (gapToLeader, interval) = driver.GapsAt(clock);
            cars.Add(new CarState(driver.DriverNumber, driver.PositionAt(clock) ?? 0, lap, gapToLeader, interval, compound, tyreAge, driver.InPitAt(clock)));
        }

        var ordered = OrderByPosition(cars);
        var weather = _session.Weather.TryGetAtOrBefore(clock, out var sample)
            ? sample
            : _session.Weather.Count > 0 ? _session.Weather[0] : NoWeather;
        var track = _session.Track.TryGetAtOrBefore(clock, out var status) ? status : ReplaySession.GreenTrack;

        return new RaceState(
            _session.SessionKey,
            clock,
            ordered.Count == 0 ? 0 : ordered.Max(car => car.Lap),
            ordered,
            weather,
            track,
            _session.RaceControl.Between(messagesAfter, clock));
    }

    /// <summary>Autos sin posición conocida (sin muestra ni grilla) van detrás de los conocidos, por número.</summary>
    private static List<CarState> OrderByPosition(List<CarState> cars)
    {
        var ordered = cars.Where(car => car.Position > 0).OrderBy(car => car.Position).ThenBy(car => car.DriverNumber).ToList();
        var next = ordered.Count == 0 ? 1 : ordered.Max(car => car.Position) + 1;
        foreach (var car in cars.Where(car => car.Position <= 0).OrderBy(car => car.DriverNumber))
        {
            ordered.Add(car with { Position = next++ });
        }

        return ordered;
    }
}
