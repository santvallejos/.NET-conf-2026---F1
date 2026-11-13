namespace PitWall.Replay;

/// <summary>La sesión pedida no tiene datos suficientes en SQLite para reproducirse.</summary>
public sealed class ReplaySessionException(string message) : Exception(message);
