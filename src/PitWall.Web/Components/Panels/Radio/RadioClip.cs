namespace PitWall.Web.Components.Panels.Radio;

/// <summary>Mensaje de radio de un piloto; <see cref="Url"/> es null si el mp3 no está en disco.</summary>
public sealed record RadioClip(int DriverNumber, DateTimeOffset Date, string? Url);
