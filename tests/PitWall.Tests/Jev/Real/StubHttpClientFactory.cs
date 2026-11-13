namespace PitWall.Tests.Jev.Real;

/// <summary>Entrega clientes sobre el handler stub, con la base de <c>Jev:BaseUrl</c> por defecto.</summary>
internal sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public static readonly Uri BaseAddress = new("https://api.typesafe.ai/v1/");

    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { BaseAddress = BaseAddress };
}
