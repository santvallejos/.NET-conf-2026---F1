using System.Threading.Channels;

using PitWall.Domain;

namespace PitWall.Web.Services;

/// <summary>
/// Cola de disparos de Jev: estrategia calculada, mensajes de Race Control, botón "Consultar Jev" y eventos
/// inyectados. La consume <see cref="JevWorker"/>.
/// </summary>
public sealed class JevTriggers
{
    private readonly Channel<JevWorkItem> _channel = Channel.CreateUnbounded<JevWorkItem>(new UnboundedChannelOptions
    {
        SingleReader = true,
    });

    internal ChannelReader<JevWorkItem> Reader => _channel.Reader;

    /// <summary>Fuerza la estrategia del piloto con su último cálculo, en cualquier modo.</summary>
    /// <param name="scenarioId">
    /// Escenario hipotético activo: se consulta con su estrategia y la decisión es hipotética. null = carrera real.
    /// </param>
    public void RequestStrategy(int driverNumber, Guid? scenarioId = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(driverNumber, 1);

        Write(new JevWorkItem.Manual(driverNumber, scenarioId));
    }

    /// <summary>Triage ante un evento inyectado; con <paramref name="scenarioId"/> la decisión es hipotética.</summary>
    /// <param name="injectedEvent">Descripción compacta del evento que viaja en <c>injected</c> del state.</param>
    /// <param name="eventId">Id del evento en el escenario; sus decisiones quedan asociadas a él en el timeline.</param>
    public void RequestTriage(string injectedEvent, Guid? scenarioId = null, int? eventId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(injectedEvent);

        Write(new JevWorkItem.Injected(injectedEvent, scenarioId, eventId));
    }

    internal void OnStrategyPublished(StrategyPublication publication) => Write(new JevWorkItem.StrategyCalculated(publication));

    internal void OnRaceControl(RaceState state) => Write(new JevWorkItem.RaceControl(state));

    private void Write(JevWorkItem item) => _channel.Writer.TryWrite(item);
}
