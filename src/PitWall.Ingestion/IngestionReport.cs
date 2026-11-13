namespace PitWall.Ingestion;

/// <summary>Filas escritas por tabla en una ingesta y estado de los mp3 de <c>team_radio</c>.</summary>
public sealed record IngestionReport
{
    public required int SessionKey { get; init; }

    public required int Drivers { get; init; }

    public required int Laps { get; init; }

    public required int Stints { get; init; }

    public required int PitStops { get; init; }

    public required int RaceControl { get; init; }

    public required int Weather { get; init; }

    public required int Intervals { get; init; }

    public required int Positions { get; init; }

    public required int Overtakes { get; init; }

    public required int SessionResults { get; init; }

    public required int StartingGrid { get; init; }

    public required int TeamRadio { get; init; }

    public required int CarData { get; init; }

    public required int Locations { get; init; }

    /// <summary>mp3 descargados en esta ejecución (los que ya estaban en disco no se vuelven a bajar).</summary>
    public required int RadioFilesDownloaded { get; init; }

    /// <summary>mp3 que OpenF1 lista pero el servidor devolvió 404.</summary>
    public required int RadioFilesMissing { get; init; }
}
