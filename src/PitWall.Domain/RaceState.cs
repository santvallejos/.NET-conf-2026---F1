namespace PitWall.Domain;

/// <summary>Instantánea del estado de carrera en un tick del replay (docs/architecture.md §4).</summary>
public sealed record RaceState(
    int SessionKey,
    DateTimeOffset Clock,
    int LeaderLap,
    IReadOnlyList<CarState> Cars,
    WeatherState Weather,
    TrackStatus Track,
    IReadOnlyList<RaceControlMessage> NewMessages);
