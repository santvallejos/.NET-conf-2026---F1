using System.Diagnostics;
using System.Diagnostics.Metrics;

using PitWall.Domain;

namespace PitWall.Jev.Guards;

/// <summary>Métricas de Jev para el dashboard de Aspire (docs/architecture.md §5).</summary>
public sealed class JevMetrics
{
    public const string MeterName = "PitWall.Jev";

    private readonly Counter<long> _requests;
    private readonly Counter<long> _tokens;
    private readonly Histogram<double> _latency;
    private readonly Counter<double> _costUsd;

    public JevMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(MeterName);
        _requests = meter.CreateCounter<long>("pitwall.jev.requests", description: "Requests a Jev resueltas por la API o la caché.");
        _tokens = meter.CreateCounter<long>("pitwall.jev.tokens", description: "Tokens de entrada enviados a Jev.");
        _latency = meter.CreateHistogram<double>("pitwall.jev.latency", unit: "ms", description: "Latencia de las llamadas a Jev.");
        _costUsd = meter.CreateCounter<double>("pitwall.jev.cost_usd", unit: "USD", description: "Costo estimado de Jev.");
    }

    public void RecordCacheHit(JevKind kind, JevClientKind client) =>
        _requests.Add(1, Tags(kind, client, cache: "hit"));

    public void RecordCall(JevKind kind, JevClientKind client, int latencyMs, int tokens, decimal costUsd)
    {
        _requests.Add(1, Tags(kind, client, cache: "miss"));

        var tags = Tags(kind, client);
        _tokens.Add(tokens, tags);
        _latency.Record(latencyMs, tags);
        _costUsd.Add((double)costUsd, tags);
    }

    private static TagList Tags(JevKind kind, JevClientKind client, string? cache = null)
    {
        var tags = new TagList
        {
            { "kind", kind == JevKind.Triage ? "triage" : "strategy" },
            { "client", client == JevClientKind.Real ? "real" : "fake" },
        };

        if (cache is not null)
        {
            tags.Add("cache", cache);
        }

        return tags;
    }
}
