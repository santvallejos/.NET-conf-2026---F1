using PitWall.Strategy.Scenarios;

namespace PitWall.Web.Services;

/// <summary>Evento a inyectar desde el panel de escenarios o el hub; los parámetros vacíos toman los defaults de <c>Scenarios:*</c>.</summary>
public sealed record InjectEventRequest(InjectedEventType Type, int Lap, InjectedEventParameters? Parameters);
