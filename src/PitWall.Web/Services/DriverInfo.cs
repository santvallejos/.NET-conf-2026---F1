namespace PitWall.Web.Services;

/// <summary>Datos de presentación de un piloto; <see cref="TeamColour"/> es un color CSS <c>#RRGGBB</c> ya validado.</summary>
public sealed record DriverInfo(int DriverNumber, string Acronym, string FullName, string Team, string TeamColour);
