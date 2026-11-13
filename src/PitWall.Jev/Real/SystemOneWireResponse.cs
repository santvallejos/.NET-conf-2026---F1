namespace PitWall.Jev.Real;

/// <summary>
/// Respuesta de <c>POST /v1/systemone</c> (schema <c>SystemOneResponse</c>). Todo es anulable para validar el
/// contrato a mano y fallar con <see cref="JevClientException"/> en vez de con una excepción de serialización.
/// </summary>
/// <param name="Model">Id versionado que respondió (p. ej. <c>jev-1.14.0</c>), aunque el request use un alias.</param>
internal sealed record SystemOneWireResponse(
    string? Model,
    IReadOnlyDictionary<string, SystemOneWireAnswer>? Answers,
    SystemOneWireUsage? Usage);
