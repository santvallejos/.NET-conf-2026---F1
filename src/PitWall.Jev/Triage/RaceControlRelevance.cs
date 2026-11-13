using System.Text.RegularExpressions;

using PitWall.Domain;

namespace PitWall.Jev.Triage;

/// <summary>
/// Mensajes de Race Control que justifican un triage para un piloto (ADR-012: triage antes que estrategia, pero
/// sin gastar en ruido). Se descartan las banderas azules, los "CLEAR" de sector y los mensajes dirigidos a otro
/// piloto, ya sea por <see cref="RaceControlMessage.DriverNumber"/> o porque el texto nombra a otros autos
/// ("CAR 51 (CAM) TIME ... DELETED", "INCIDENT INVOLVING CARS 23 (ALB) AND 10 (GAS)").
/// </summary>
public static partial class RaceControlRelevance
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

        if (Is(message.Flag, "CLEAR") && Is(message.Scope, "Sector"))
        {
            return false;
        }

        // OpenF1 deja sin DriverNumber los track limits, investigaciones y penalidades: el auto viaja en el texto.
        var mentioned = MentionedCars(message.Message);
        return mentioned.Count == 0 || mentioned.Contains(driverNumber);
    }

    private static HashSet<int> MentionedCars(string text)
    {
        var cars = new HashSet<int>();
        foreach (Match match in CarNumber().Matches(text))
        {
            cars.Add(int.Parse(match.Groups["number"].ValueSpan, provider: null));
        }

        if (cars.Count > 0)
        {
            foreach (Match match in AdditionalCarNumber().Matches(text))
            {
                cars.Add(int.Parse(match.Groups["number"].ValueSpan, provider: null));
            }
        }

        return cars;
    }

    private static bool Is(string? value, string expected) => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"\bCARS?\s+(?<number>\d{1,2})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CarNumber();

    // Autos siguientes en la misma frase: "CARS 23 (ALB) AND 10 (GAS)", "CARS 1 (VER), 4 (NOR) AND 16 (LEC)".
    [GeneratedRegex(@"(?:\bAND|,)\s+(?<number>\d{1,2})\s*\([A-Z]{3}\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AdditionalCarNumber();
}
