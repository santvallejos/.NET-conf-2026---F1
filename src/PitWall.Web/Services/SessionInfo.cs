namespace PitWall.Web.Services;

/// <summary>Sesión ingerida disponible para reproducir.</summary>
public sealed record SessionInfo(int SessionKey, string Name, string Circuit, DateTimeOffset DateStart)
{
    public string Label => $"{Circuit} · {Name} ({DateStart:yyyy-MM-dd}) · {SessionKey}";
}
