namespace PitWall.Tests.Jev.Real;

internal sealed record RecordedRequest(
    HttpMethod Method,
    Uri? Uri,
    string? Authorization,
    string? ContentType,
    string Body);
