using System.Globalization;
using System.Text.RegularExpressions;

using PitWall.Domain;

namespace PitWall.Strategy.Degradation;

/// <summary>
/// Períodos de carrera con bandera amarilla (de pista o de sector), roja, SC o VSC, derivados de Race Control.
/// Una vuelta que se solapa con alguno no es limpia para la regresión de degradación.
/// </summary>
public sealed partial class NeutralisationTimeline
{
    private readonly List<(DateTimeOffset Start, DateTimeOffset End)> _periods = [];

    /// <summary>
    /// Interpretación de OpenF1: amarilla o roja de alcance <c>Track</c> hasta la siguiente verde, "CLEAR" o a
    /// cuadros; amarilla de sector hasta su "CLEAR" o una verde de pista; SC y VSC desde su "DEPLOYED" hasta la
    /// verde de pista (el VSC también termina con "VIRTUAL SAFETY CAR ENDING").
    /// </summary>
    public NeutralisationTimeline(IEnumerable<RaceControlMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var trackFlag = false;
        var safetyCar = false;
        var virtualSafetyCar = false;
        var sectors = new HashSet<int>();
        DateTimeOffset? openSince = null;

        foreach (var message in messages.OrderBy(m => m.Date))
        {
            var flag = message.Flag?.Trim().ToUpperInvariant();
            var text = message.Message.ToUpperInvariant();
            if (IsCategory(message, "Flag") && IsScope(message, "Track") && flag is not null)
            {
                if (flag is "GREEN" or "CLEAR" or "CHEQUERED")
                {
                    trackFlag = false;
                    safetyCar = false;
                    virtualSafetyCar = false;
                    sectors.Clear();
                }
                else if (flag is "YELLOW" or "DOUBLE YELLOW" or "RED")
                {
                    trackFlag = true;
                }
            }
            else if (IsCategory(message, "Flag") && IsScope(message, "Sector") && flag is not null
                && SectorPattern().Match(message.Message) is { Success: true } match)
            {
                // El sector viene solo en el texto ("YELLOW IN TRACK SECTOR 5").
                var sector = int.Parse(match.Groups[1].ValueSpan, CultureInfo.InvariantCulture);
                if (flag is "YELLOW" or "DOUBLE YELLOW")
                {
                    sectors.Add(sector);
                }
                else if (flag == "CLEAR")
                {
                    sectors.Remove(sector);
                }
            }
            else if (IsCategory(message, "SafetyCar"))
            {
                // "VIRTUAL SAFETY CAR DEPLOYED" contiene "SAFETY CAR DEPLOYED": el VSC se evalúa primero.
                if (text.Contains("VIRTUAL SAFETY CAR DEPLOYED", StringComparison.Ordinal))
                {
                    virtualSafetyCar = true;
                }
                else if (text.Contains("VIRTUAL SAFETY CAR ENDING", StringComparison.Ordinal))
                {
                    virtualSafetyCar = false;
                }
                else if (text.Contains("SAFETY CAR DEPLOYED", StringComparison.Ordinal))
                {
                    safetyCar = true;
                }
            }

            var active = trackFlag || safetyCar || virtualSafetyCar || sectors.Count > 0;
            if (active && openSince is null)
            {
                openSince = message.Date;
            }
            else if (!active && openSince is { } start)
            {
                _periods.Add((start, message.Date));
                openSince = null;
            }
        }

        if (openSince is { } open)
        {
            _periods.Add((open, DateTimeOffset.MaxValue));
        }
    }

    /// <summary>True si algún período neutralizado se solapa con [<paramref name="from"/>, <paramref name="to"/>].</summary>
    public bool IsNeutralised(DateTimeOffset from, DateTimeOffset to) =>
        _periods.Any(period => period.Start <= to && period.End > from);

    private static bool IsCategory(RaceControlMessage message, string category) =>
        string.Equals(message.Category, category, StringComparison.OrdinalIgnoreCase);

    private static bool IsScope(RaceControlMessage message, string scope) =>
        string.Equals(message.Scope, scope, StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"SECTOR\s+(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SectorPattern();
}
