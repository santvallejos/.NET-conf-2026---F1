namespace PitWall.Domain;

/// <summary>Mensaje de Race Control; campos de la tabla <c>RaceControl</c> (docs/data-openf1.md §4).</summary>
public sealed record RaceControlMessage(
    DateTimeOffset Date,
    int? LapNumber,
    string Category,
    string? Flag,
    string? Scope,
    int? DriverNumber,
    string Message);
