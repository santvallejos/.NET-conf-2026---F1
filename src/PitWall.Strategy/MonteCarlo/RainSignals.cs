using System.Globalization;
using System.Text.RegularExpressions;

using PitWall.Domain;

namespace PitWall.Strategy.MonteCarlo;

/// <summary>Riesgo de lluvia anunciado por Race Control ("RISK OF RAIN FOR F1 RACE IS 40%").</summary>
public static partial class RainSignals
{
    /// <summary>
    /// Probabilidad (0..1) del último "RISK OF RAIN x%" en orden cronológico; 0 si no hay. Pasar solo los mensajes
    /// emitidos hasta el instante que se evalúa.
    /// </summary>
    public static double RainRisk(IEnumerable<RaceControlMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var risk = 0.0;
        foreach (var message in messages.OrderBy(m => m.Date))
        {
            if (RiskPattern().Match(message.Message) is { Success: true } match)
            {
                risk = Math.Clamp(int.Parse(match.Groups[1].ValueSpan, CultureInfo.InvariantCulture) / 100.0, 0, 1);
            }
        }

        return risk;
    }

    [GeneratedRegex(@"RISK OF RAIN\D*?(\d{1,3})\s*%", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RiskPattern();
}
