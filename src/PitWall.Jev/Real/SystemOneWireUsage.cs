namespace PitWall.Jev.Real;

/// <summary>Consumo de la llamada (schema <c>Usage</c>). Solo los tokens de entrada se facturan.</summary>
internal sealed record SystemOneWireUsage(int? InputTokens, int? OutputTokens);
