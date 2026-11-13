using System.Globalization;
using System.Text;
using System.Text.Json;

namespace PitWall.Strategy.Scenarios;

/// <summary>Evento hipotético de un escenario; afecta solo la decisión, nunca el replay (ADR-010).</summary>
/// <param name="Lap">Vuelta de carrera (la del líder) en la que ocurre.</param>
/// <param name="Parameters">Parámetros con los defaults de <c>Scenarios:*</c> ya aplicados.</param>
public sealed record InjectedEvent(
    int Id,
    Guid ScenarioId,
    InjectedEventType Type,
    int Lap,
    InjectedEventParameters Parameters,
    DateTimeOffset CreatedAt)
{
    /// <summary>Descripción compacta del evento para el <c>injected</c> del state de Jev, p. ej. <c>SAFETY_CAR lap 40 for 4 laps</c>.</summary>
    public string Describe()
    {
        var text = new StringBuilder(JsonNamingPolicy.SnakeCaseUpper.ConvertName(Type.ToString()));
        var parameters = Parameters;
        if (parameters.Intensity is { } intensity)
        {
            text.Append(CultureInfo.InvariantCulture, $" {Upper(intensity)}");
        }

        if (parameters.Severity is { } severity)
        {
            text.Append(CultureInfo.InvariantCulture, $" {Upper(severity)}");
        }

        if (parameters.Rival is { } rival)
        {
            text.Append(CultureInfo.InvariantCulture, $" car {rival}");
            if (parameters.Relative is { } relative && parameters.Driver is { } reference)
            {
                text.Append(CultureInfo.InvariantCulture, $" {Upper(relative)} of car {reference}");
            }
        }
        else if (parameters.Driver is { } driver)
        {
            text.Append(CultureInfo.InvariantCulture, $" car {driver}");
        }

        text.Append(CultureInfo.InvariantCulture, $" lap {Lap}");
        if (parameters.DurationLaps is { } duration)
        {
            text.Append(CultureInfo.InvariantCulture, $" for {duration} laps");
        }

        return text.ToString();
    }

    private static string Upper<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        JsonNamingPolicy.SnakeCaseUpper.ConvertName(value.ToString());
}
