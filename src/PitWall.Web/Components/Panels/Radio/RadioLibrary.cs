using System.Data.Common;
using System.Globalization;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using PitWall.Data;
using PitWall.Web.Services;

namespace PitWall.Web.Components.Panels.Radio;

/// <summary>
/// Mensajes de radio de la sesión (tabla <c>TeamRadio</c>) y resolución segura de sus mp3 locales, que la ingesta
/// deja en <c>{PitWall:RadioPath}/{session_key}/</c>.
/// </summary>
public sealed class RadioLibrary
{
    private const string SolutionFileName = "PitWall.sln";
    private const string Mp3Extension = ".mp3";

    private readonly IDbContextFactory<PitWallDbContext> _dbContextFactory;
    private readonly ILogger<RadioLibrary> _logger;
    private readonly string _root;
    private readonly Lock _gate = new();
    private (int SessionKey, IReadOnlyList<RadioClip> Clips)? _clips;

    public RadioLibrary(
        IDbContextFactory<PitWallDbContext> dbContextFactory,
        IOptions<PitWallOptions> options,
        IHostEnvironment environment,
        ILogger<RadioLibrary> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(logger);

        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _root = Path.GetFullPath(ResolveRoot(options.Value.RadioPath, environment.ContentRootPath));
    }

    public bool IsLoaded(int sessionKey)
    {
        lock (_gate)
        {
            return _clips?.SessionKey == sessionKey;
        }
    }

    /// <summary>Mensajes de la sesión cargada en orden cronológico; vacío si es otra sesión.</summary>
    public IReadOnlyList<RadioClip> Get(int sessionKey)
    {
        lock (_gate)
        {
            return _clips is { } clips && clips.SessionKey == sessionKey ? clips.Clips : [];
        }
    }

    /// <summary>Lee los mensajes de la sesión; si la base no se puede leer, la deja sin mensajes.</summary>
    public async Task LoadAsync(int sessionKey, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<RadioClip> clips;
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var rows = await db.TeamRadio.AsNoTracking().Where(e => e.SessionKey == sessionKey).ToListAsync(cancellationToken);
            clips = [.. rows
                .OrderBy(e => e.Date)
                .Select(e => new RadioClip(e.DriverNumber, e.Date, UrlFor(sessionKey, e.LocalPath)))];
        }
        catch (DbException exception)
        {
            _logger.LogWarning(exception, "Could not read the team radio of session {SessionKey}", sessionKey);
            clips = [];
        }

        lock (_gate)
        {
            _clips = (sessionKey, clips);
        }
    }

    /// <summary>
    /// Ruta absoluta del mp3 <paramref name="fileName"/> de la sesión, o null si no existe o el nombre intenta salir
    /// de la carpeta de la sesión.
    /// </summary>
    public string? ResolveFile(int sessionKey, string fileName)
    {
        if (string.IsNullOrEmpty(fileName)
            || fileName != Path.GetFileName(fileName)
            || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || !fileName.EndsWith(Mp3Extension, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var directory = Path.Combine(_root, sessionKey.ToString(CultureInfo.InvariantCulture)) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(directory, fileName));
        return path.StartsWith(directory, StringComparison.OrdinalIgnoreCase) && File.Exists(path) ? path : null;
    }

    // La ingesta guarda LocalPath como "{session_key}/{archivo}", relativo a la raíz de radio.
    private string? UrlFor(int sessionKey, string localPath)
    {
        var fileName = Path.GetFileName(localPath);
        return ResolveFile(sessionKey, fileName) is null
            ? null
            : $"{RadioEndpoints.RoutePrefix}/{sessionKey.ToString(CultureInfo.InvariantCulture)}/{Uri.EscapeDataString(fileName)}";
    }

    // Igual que la base: una ruta relativa se resuelve desde la raíz del repo para leer los mismos mp3 que dejó la
    // ingesta aunque la app corra con el directorio del proyecto como directorio actual.
    private static string ResolveRoot(string configuredPath, string contentRootPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        for (var current = new DirectoryInfo(contentRootPath); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, SolutionFileName)))
            {
                return Path.Combine(current.FullName, configuredPath);
            }
        }

        return Path.Combine(contentRootPath, configuredPath);
    }
}
