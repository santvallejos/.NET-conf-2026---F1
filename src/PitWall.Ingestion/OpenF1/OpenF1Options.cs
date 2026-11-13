namespace PitWall.Ingestion.OpenF1;

/// <summary>Acceso a OpenF1: el rate limit no está documentado, así que se pausa entre requests y se reintenta con backoff.</summary>
public sealed class OpenF1Options
{
    public static readonly Uri DefaultBaseAddress = new("https://api.openf1.org/v1/");

    public Uri BaseAddress { get; init; } = DefaultBaseAddress;

    /// <summary>Pausa antes de cada request salvo el primero.</summary>
    public TimeSpan RequestDelay { get; init; } = IngestionOptions.DefaultRequestDelay;

    /// <summary>Reintentos ante 429/5xx; el request se envía como mucho <c>MaxRetries + 1</c> veces.</summary>
    public int MaxRetries { get; init; } = 4;

    /// <summary>Espera del primer reintento; se duplica en cada uno salvo que el servidor mande <c>Retry-After</c>.</summary>
    public TimeSpan InitialBackoff { get; init; } = TimeSpan.FromSeconds(1);
}
