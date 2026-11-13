using System.Globalization;

using PitWall.Domain;
using PitWall.Strategy.Timing;

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

    /// <summary>Tiempo de sector en segundos con milésimas (<c>21.428</c>); guion sin dato.</summary>
    public static string SectorTime(double? seconds) =>
        seconds is { } value ? value.ToString("0.000", CultureInfo.InvariantCulture) : Missing;

    /// <summary>Tiempo de vuelta <c>m:ss.fff</c> (o <c>ss.fff</c> bajo el minuto); guion sin dato.</summary>
    public static string LapTime(double? seconds) =>
        seconds is { } value
            ? TimeSpan.FromSeconds(value).ToString(value >= 60 ? @"m\:ss\.fff" : @"ss\.fff", CultureInfo.InvariantCulture)
            : Missing;

    /// <summary>Clase global de color de timing (docs/ui.md §1: púrpura, verde, amarillo); <c>null</c> sin dato.</summary>
    public static string? TimingClassName(TimingClass? timing) => timing switch
    {
        TimingClass.SessionBest => "timing-session-best",
        TimingClass.PersonalBest => "timing-personal-best",
        TimingClass.NoImprovement => "timing-no-improvement",
        _ => null,
    };

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
