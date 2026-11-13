namespace PitWall.Jev.Real;

/// <summary>Cuerpo de <c>POST /v1/systemone</c> (schema <c>SystemOneRequest</c> de la API de TypeSafe).</summary>
internal sealed record SystemOneWireRequest(
    string Model,
    string State,
    IReadOnlyDictionary<string, SystemOneWireQuestion> Questions);
