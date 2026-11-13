using System.Globalization;

using PitWall.Domain;

namespace PitWall.Web.Components.Shared;

/// <summary>Formatos y clases CSS comunes de los paneles (números invariantes, monoespaciados en CSS).</summary>
public static class RaceFormat
{
    public const string Missing = "—";

    public static string Gap(double? seconds) =>
        seconds is { } value ? $"+{value.ToString("0.000", CultureInfo.InvariantCulture)}" : Missing;

    public static string Number(double value, string format = "0.0") => value.ToString(format, CultureInfo.InvariantCulture);

    public static string Elapsed(TimeSpan elapsed) =>
        $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";

    public static string TimeOfDay(DateTimeOffset date) => date.UtcDateTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    public static string CompoundLetter(Compound compound) => compound switch
    {
        Compound.Soft => "S",
        Compound.Medium => "M",
        Compound.Hard => "H",
        Compound.Intermediate => "I",
        Compound.Wet => "W",
        _ => "?",
    };

    public static string CompoundClass(Compound compound) => $"tyre-{compound.ToString().ToLowerInvariant()}";

    /// <summary>Clase CSS del color de una bandera de OpenF1 (<c>GREEN</c>, <c>DOUBLE YELLOW</c>, …); vacía si no hay.</summary>
    public static string FlagClass(string? flag) =>
        string.IsNullOrWhiteSpace(flag) ? string.Empty : $"flag-{flag.Trim().ToLowerInvariant().Replace(' ', '-')}";
}
