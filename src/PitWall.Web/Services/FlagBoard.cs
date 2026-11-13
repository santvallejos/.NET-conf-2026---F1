using System.Globalization;
using System.Text.RegularExpressions;

using PitWall.Domain;

namespace PitWall.Web.Services;

/// <summary>Banderas activas en un instante: las de pista (<see cref="TrackStatus"/>) más las de sector y las azules.</summary>
public sealed partial record FlagBoard(
    string TrackFlag,
    bool SafetyCar,
    bool VirtualSafetyCar,
    IReadOnlyList<int> YellowSectors,
    IReadOnlyList<int> DoubleYellowSectors,
    IReadOnlyList<int> BlueFlagDrivers)
{
    /// <summary>Una bandera azul se muestra como activa durante este tiempo de carrera desde su mensaje.</summary>
    public static readonly TimeSpan BlueFlagWindow = TimeSpan.FromSeconds(30);

    public static FlagBoard Empty { get; } = new("GREEN", SafetyCar: false, VirtualSafetyCar: false, [], [], []);

    public bool Red => TrackFlag == "RED";

    public bool Chequered => TrackFlag == "CHEQUERED";

    public bool Yellow => TrackFlag == "YELLOW" || YellowSectors.Count > 0;

    public bool DoubleYellow => TrackFlag == "DOUBLE YELLOW" || DoubleYellowSectors.Count > 0;

    public bool Blue => BlueFlagDrivers.Count > 0;

    public bool Green => !Red && !Chequered && !Yellow && !DoubleYellow && !SafetyCar && !VirtualSafetyCar;

    /// <summary>
    /// Arma el tablero desde los mensajes de Race Control con fecha ≤ <paramref name="clock"/> (en orden cronológico).
    /// Interpretación de OpenF1: el sector viene solo en el texto ("... IN TRACK SECTOR 5"), "CLEAR" lo libera y
    /// una bandera de alcance <c>Track</c> verde, roja o a cuadros libera todos los sectores.
    /// </summary>
    public static FlagBoard Create(TrackStatus track, IEnumerable<RaceControlMessage> messages, DateTimeOffset clock)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(messages);

        var sectors = new SortedDictionary<int, string>();
        var blue = new SortedSet<int>();
        foreach (var message in messages)
        {
            if (!string.Equals(message.Category, "Flag", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(message.Flag))
            {
                continue;
            }

            var flag = message.Flag.ToUpperInvariant();
            if (string.Equals(message.Scope, "Track", StringComparison.OrdinalIgnoreCase) && flag is "GREEN" or "RED" or "CHEQUERED")
            {
                sectors.Clear();
            }
            else if (string.Equals(message.Scope, "Sector", StringComparison.OrdinalIgnoreCase) && TryGetSector(message.Message, out var sector))
            {
                if (flag is "YELLOW" or "DOUBLE YELLOW")
                {
                    sectors[sector] = flag;
                }
                else if (flag == "CLEAR")
                {
                    sectors.Remove(sector);
                }
            }
            else if (flag == "BLUE" && message.DriverNumber is { } driver && message.Date > clock - BlueFlagWindow)
            {
                blue.Add(driver);
            }
        }

        return new FlagBoard(
            track.Flag.ToUpperInvariant(),
            track.SafetyCar,
            track.VirtualSafetyCar,
            [.. sectors.Where(pair => pair.Value == "YELLOW").Select(pair => pair.Key)],
            [.. sectors.Where(pair => pair.Value == "DOUBLE YELLOW").Select(pair => pair.Key)],
            [.. blue]);
    }

    private static bool TryGetSector(string text, out int sector)
    {
        var match = SectorPattern().Match(text);
        sector = match.Success ? int.Parse(match.Groups[1].ValueSpan, CultureInfo.InvariantCulture) : 0;
        return match.Success;
    }

    [GeneratedRegex(@"SECTOR\s+(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SectorPattern();
}
