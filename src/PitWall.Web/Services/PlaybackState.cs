namespace PitWall.Web.Services;

/// <summary>Estado del reproductor: sesión cargada, play/pausa, velocidad y vueltas disponibles.</summary>
public sealed record PlaybackState(
    int? SessionKey,
    bool IsPlaying,
    int Speed,
    IReadOnlyList<int> Speeds,
    int LapCount,
    DateTimeOffset? StartTime,
    DateTimeOffset? EndTime);
