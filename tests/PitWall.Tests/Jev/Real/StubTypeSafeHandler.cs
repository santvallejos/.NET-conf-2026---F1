using System.Net;
using System.Text;

namespace PitWall.Tests.Jev.Real;

/// <summary>Reemplaza a la API de TypeSafe: guarda cada request y responde lo que el test programa.</summary>
internal sealed class StubTypeSafeHandler : HttpMessageHandler
{
    private readonly Func<CancellationToken, Task<HttpResponseMessage>> _respond;

    public StubTypeSafeHandler(Func<CancellationToken, Task<HttpResponseMessage>> respond) => _respond = respond;

    public List<RecordedRequest> Requests { get; } = [];

    public static StubTypeSafeHandler Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(_ => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        }));

    /// <summary>No responde nunca: solo termina cuando se cancela el request.</summary>
    public static StubTypeSafeHandler Hanging() =>
        new(async ct =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("Unreachable.");
        });

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new RecordedRequest(
            request.Method,
            request.RequestUri,
            request.Headers.Authorization?.ToString(),
            request.Content?.Headers.ContentType?.MediaType,
            body));

        return await _respond(cancellationToken);
    }
}
