using Microsoft.Extensions.Time.Testing;

using PitWall.Jev;
using PitWall.Jev.Fake;

namespace PitWall.Tests.Jev;

/// <summary><see cref="FakeJevClient"/> que cuenta llamadas y simula latencia avanzando el reloj.</summary>
internal sealed class CountingJevClient(FakeTimeProvider timeProvider) : IJevClient
{
    private readonly FakeJevClient _inner = new();

    public int Calls { get; private set; }

    public TimeSpan SimulatedLatency { get; init; } = TimeSpan.FromMilliseconds(212);

    public int? Tokens { get; init; }

    public bool Fail { get; set; }

    public async Task<JevResponse> SystemOneAsync(JevRequest request, CancellationToken ct)
    {
        Calls++;
        if (Fail)
        {
            throw new JevClientException("Sin red.");
        }

        timeProvider.Advance(SimulatedLatency);
        var response = await _inner.SystemOneAsync(request, ct);
        return Tokens is { } tokens ? response with { Tokens = tokens } : response;
    }
}
