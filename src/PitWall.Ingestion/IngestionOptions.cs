namespace PitWall.Ingestion;

/// <summary>Parámetros de una ejecución de la CLI de ingesta (ver <see cref="IngestionArguments"/>).</summary>
public sealed record IngestionOptions
{
    public const string DefaultDatabasePath = "data/pitwall.db";

    public const string DefaultRadioPath = "data/radio";

    public const string DefaultFixturesPath = "data/fixtures/openf1";

    public static readonly TimeSpan DefaultRequestDelay = TimeSpan.FromMilliseconds(500);

    public required int SessionKey { get; init; }

    /// <summary>Sesión de clasificación de la que se toma <c>starting_grid</c> (OpenF1 devuelve 404 con la key de carrera).</summary>
    public int? QualiSessionKey { get; init; }

    /// <summary>Pilotos para los que se descargan <c>car_data</c> y <c>location</c>; vacío = sin telemetría.</summary>
    public IReadOnlyList<int> Drivers { get; init; } = [];

    public string DatabasePath { get; init; } = DefaultDatabasePath;

    /// <summary>Raíz de los mp3; cada sesión va en <c>{RadioPath}/{SessionKey}/</c>.</summary>
    public string RadioPath { get; init; } = DefaultRadioPath;

    public TimeSpan RequestDelay { get; init; } = DefaultRequestDelay;

    /// <summary>Si no es <c>null</c>, cada respuesta JSON de OpenF1 se graba en esta carpeta como fixture.</summary>
    public string? FixturesPath { get; init; }
}
