namespace PitWall.Web.Components.Panels.Radio;

public static class RadioEndpoints
{
    public const string RoutePrefix = "/radio";

    /// <summary>
    /// Sirve los mp3 de <c>{PitWall:RadioPath}/{session_key}/</c> en <c>/radio/{sessionKey}/{fileName}</c>, sin
    /// copiarlos a wwwroot. Con rangos para que el reproductor pueda adelantar.
    /// </summary>
    public static IEndpointRouteBuilder MapPitWallRadio(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet($"{RoutePrefix}/{{sessionKey:int}}/{{fileName}}", (int sessionKey, string fileName, RadioLibrary radio) =>
            radio.ResolveFile(sessionKey, fileName) is { } path
                ? Results.File(path, "audio/mpeg", enableRangeProcessing: true)
                : Results.NotFound());
        return endpoints;
    }
}
