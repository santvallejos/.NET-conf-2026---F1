using PitWall.Domain;

namespace PitWall.Strategy.Rules;

/// <summary>Señales derivadas de los mensajes de Race Control.</summary>
public static class RaceControlSignals
{
    private static readonly string[] ClosedMarkers = ["PIT LANE CLOSED", "PIT EXIT CLOSED", "PIT ENTRY CLOSED"];
    private static readonly string[] OpenMarkers = ["PIT LANE OPEN", "PIT EXIT OPEN", "PIT ENTRY OPEN"];

    /// <summary>True si el último mensaje de apertura/cierre del pit lane, en orden cronológico, lo cerró.</summary>
    public static bool IsPitLaneClosed(IEnumerable<RaceControlMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var closed = false;
        foreach (var message in messages.OrderBy(m => m.Date))
        {
            if (ContainsAny(message.Message, ClosedMarkers))
            {
                closed = true;
            }
            else if (ContainsAny(message.Message, OpenMarkers))
            {
                closed = false;
            }
        }

        return closed;
    }

    private static bool ContainsAny(string text, string[] markers) =>
        markers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));
}
