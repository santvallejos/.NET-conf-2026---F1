using PitWall.Domain;

namespace PitWall.Strategy.Energy;

/// <summary>Estado del overtake mode según Race Control.</summary>
public static class OvertakeSignals
{
    private const string DisabledMarker = "OVERTAKE DISABLED";
    private const string EnabledMarker = "OVERTAKE ENABLED";

    /// <summary>
    /// True si el último mensaje de overtake, en orden cronológico, lo deshabilitó. Pasar solo los mensajes
    /// emitidos hasta el instante que se evalúa.
    /// </summary>
    public static bool IsOvertakeDisabled(IEnumerable<RaceControlMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var disabled = false;
        foreach (var message in messages.OrderBy(m => m.Date))
        {
            if (message.Message.Contains(DisabledMarker, StringComparison.OrdinalIgnoreCase))
            {
                disabled = true;
            }
            else if (message.Message.Contains(EnabledMarker, StringComparison.OrdinalIgnoreCase))
            {
                disabled = false;
            }
        }

        return disabled;
    }
}
