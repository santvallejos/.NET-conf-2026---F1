namespace PitWall.Jev.Guards;

/// <summary>Alerta de Jev para la UI; sin texto libre, la UI lo deriva de <see cref="Kind"/>.</summary>
public sealed record JevAlert(JevAlertKind Kind, int SessionKey, DateTimeOffset RaisedAt);
