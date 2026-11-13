using System.Net;

namespace PitWall.Tests.Ingestion;

/// <summary>Devuelve las respuestas en el orden dado y después 200 con <c>[]</c>; cuenta los envíos.</summary>
internal sealed class ScriptedHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new(responses);

    public int Calls { get; private set; }

    public static HttpResponseMessage Status(HttpStatusCode statusCode) => new(statusCode) { Content = new StringContent("[]") };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(_responses.TryDequeue(out var response) ? response : Status(HttpStatusCode.OK));
    }
}
