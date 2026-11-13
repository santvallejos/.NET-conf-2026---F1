using System.Globalization;

namespace PitWall.Ingestion;

/// <summary>Parser de la línea de comandos de la CLI de ingesta.</summary>
public static class IngestionArguments
{
    public const string Usage =
        """
        Uso: PitWall.Ingestion --session <session_key> [--quali <session_key>] [--drivers 43,44,12,3]
                               [--db data/pitwall.db] [--radio data/radio] [--delay-ms 500]
                               [--record-fixtures [carpeta]]

          --session          session_key de OpenF1 a descargar (obligatorio).
          --quali            session_key de la clasificación; de ahí sale starting_grid.
          --drivers          pilotos con car_data y location (ventanas de 10 min). Sin valor: sin telemetría.
          --db               ruta del SQLite (default data/pitwall.db).
          --radio            carpeta raíz de los mp3 de team_radio (default data/radio).
          --delay-ms         pausa entre requests en ms (default 500).
          --record-fixtures  graba cada respuesta JSON en la carpeta (default data/fixtures/openf1).
        """;

    /// <exception cref="ArgumentException">Argumento desconocido, sin valor o con formato inválido.</exception>
    public static IngestionOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        int? session = null;
        int? quali = null;
        IReadOnlyList<int> drivers = [];
        var database = IngestionOptions.DefaultDatabasePath;
        var radio = IngestionOptions.DefaultRadioPath;
        var delay = IngestionOptions.DefaultRequestDelay;
        string? fixtures = null;

        for (var index = 0; index < args.Count; index++)
        {
            var name = args[index];
            switch (name)
            {
                case "--session":
                    session = ParseKey(name, RequireValue(args, ref index));
                    break;
                case "--quali":
                    quali = ParseKey(name, RequireValue(args, ref index));
                    break;
                case "--drivers":
                    drivers = ParseDrivers(RequireValue(args, ref index));
                    break;
                case "--db":
                    database = RequireValue(args, ref index);
                    break;
                case "--radio":
                    radio = RequireValue(args, ref index);
                    break;
                case "--delay-ms":
                    delay = TimeSpan.FromMilliseconds(ParseNonNegative(name, RequireValue(args, ref index)));
                    break;
                case "--record-fixtures":
                    fixtures = HasValue(args, index) ? args[++index] : IngestionOptions.DefaultFixturesPath;
                    break;
                default:
                    throw new ArgumentException($"Argumento desconocido: {name}.", nameof(args));
            }
        }

        if (session is null)
        {
            throw new ArgumentException("Falta --session.", nameof(args));
        }

        return new IngestionOptions
        {
            SessionKey = session.Value,
            QualiSessionKey = quali,
            Drivers = drivers,
            DatabasePath = database,
            RadioPath = radio,
            RequestDelay = delay,
            FixturesPath = fixtures,
        };
    }

    private static bool HasValue(IReadOnlyList<string> args, int index) =>
        index + 1 < args.Count && !args[index + 1].StartsWith("--", StringComparison.Ordinal);

    private static string RequireValue(IReadOnlyList<string> args, ref int index)
    {
        if (!HasValue(args, index))
        {
            throw new ArgumentException($"{args[index]} requiere un valor.", nameof(args));
        }

        return args[++index];
    }

    private static int ParseKey(string name, string value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var key) && key > 0
            ? key
            : throw new ArgumentException($"{name} debe ser un entero positivo: {value}.", nameof(value));

    private static int ParseNonNegative(string name, string value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? number
            : throw new ArgumentException($"{name} debe ser un entero no negativo: {value}.", nameof(value));

    private static int[] ParseDrivers(string value) =>
        [.. value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(number => ParseKey("--drivers", number))
            .Distinct()];
}
