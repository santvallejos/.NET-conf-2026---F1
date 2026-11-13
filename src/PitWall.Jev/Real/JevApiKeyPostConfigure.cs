using Microsoft.Extensions.Options;

namespace PitWall.Jev.Real;

/// <summary>
/// Con <c>Jev:Client = Real</c> y sin <c>Jev:ApiKey</c>, Jev queda deshabilitado (docs/configuration.md §2, ADR-011).
/// Corre después de los overrides en caliente, así que encenderlo desde la UI no lo habilita sin key.
/// </summary>
public sealed class JevApiKeyPostConfigure : IPostConfigureOptions<JevOptions>
{
    public static bool IsMissingApiKey(JevOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Client == JevClientKind.Real && string.IsNullOrWhiteSpace(options.ApiKey);
    }

    public void PostConfigure(string? name, JevOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (name == Options.DefaultName && IsMissingApiKey(options))
        {
            options.Enabled = false;
        }
    }
}
