using System.Net;
using System.Text;

using PitWall.Ingestion.OpenF1;

namespace PitWall.Tests.Ingestion;

/// <summary>
/// Stub de OpenF1 sin red: sirve <c>Fixtures/openf1/{OpenF1FixtureRecorder.FileNameFor(uri)}</c> y responde 404
/// ("sin resultados", como OpenF1) si el fixture no existe. Los mp3 de otros hosts devuelven bytes fijos.
/// </summary>
internal sealed class OpenF1FixtureHandler : HttpMessageHandler
{
    public static readonly byte[] RecordingBytes = Encoding.ASCII.GetBytes("ID3-fake-mp3");

    private readonly List<Uri> _requests = [];

    public IReadOnlyList<Uri> Requests => _requests;

    /// <summary>Nombres de mp3 a los que se responde 404.</summary>
    public HashSet<string> MissingRecordings { get; } = new(StringComparer.Ordinal);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        _requests.Add(uri);

        if (uri.Host != OpenF1Options.DefaultBaseAddress.Host)
        {
            var fileName = Path.GetFileName(Uri.UnescapeDataString(uri.AbsolutePath));
            return Task.FromResult(MissingRecordings.Contains(fileName)
                ? NotFound()
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(RecordingBytes) });
        }

        var path = Path.Combine(OpenF1Fixtures.Directory, OpenF1FixtureRecorder.FileNameFor(uri));
        return Task.FromResult(File.Exists(path)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(File.ReadAllText(path), Encoding.UTF8, "application/json") }
            : NotFound());
    }

    private static HttpResponseMessage NotFound() =>
        new(HttpStatusCode.NotFound) { Content = new StringContent("""{"detail":"No results found."}""", Encoding.UTF8, "application/json") };
}
