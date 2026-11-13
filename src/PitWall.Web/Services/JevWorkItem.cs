using PitWall.Domain;

namespace PitWall.Web.Services;

/// <summary>Pedido para <see cref="JevWorker"/>; se procesan de a uno, en orden de llegada.</summary>
internal abstract record JevWorkItem
{
    /// <summary>Estrategia recién calculada al cerrar una vuelta.</summary>
    public sealed record StrategyCalculated(StrategyPublication Publication) : JevWorkItem;

    /// <summary>Mensajes nuevos de Race Control en un estado del replay.</summary>
    public sealed record RaceControl(RaceState State) : JevWorkItem;

    /// <summary>Botón "Consultar Jev".</summary>
    public sealed record Manual(int DriverNumber) : JevWorkItem;

    /// <summary>Evento inyectado: triage para cada piloto con Jev activo.</summary>
    /// <param name="EventId">Id del evento en el escenario, para ubicar las decisiones en su timeline.</param>
    public sealed record Injected(string Event, Guid? ScenarioId, int? EventId = null) : JevWorkItem;
}
