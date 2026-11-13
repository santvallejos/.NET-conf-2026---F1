namespace PitWall.Jev.Guards;

/// <summary>Cliente registrado para <c>Jev:Client = Real</c> mientras no haya uno real en el host.</summary>
public sealed class UnavailableJevClient : IJevClient
{
    public Task<JevResponse> SystemOneAsync(JevRequest request, CancellationToken ct) =>
        Task.FromException<JevResponse>(new JevClientException("No hay un cliente Jev real registrado."));
}
