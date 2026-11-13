using PitWall.Domain;

namespace PitWall.Jev.Triage;

/// <summary>
/// Mensajes de Race Control que justifican un triage para un piloto (ADR-012: triage antes que estrategia, pero
/// sin gastar en ruido). Se descartan las banderas azules, los "CLEAR" de sector y los mensajes dirigidos a otro
/// piloto.
/// </summary>
public static class RaceControlRelevance
{
    public static bool IsRelevant(RaceControlMessage message, int driverNumber)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.DriverNumber is { } target && target != driverNumber)
        {
            return false;
        }

        if (Is(message.Flag, "BLUE"))
        {
            return false;
        }

        return !(Is(message.Flag, "CLEAR") && Is(message.Scope, "Sector"));
    }

    private static bool Is(string? value, string expected) => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
}
