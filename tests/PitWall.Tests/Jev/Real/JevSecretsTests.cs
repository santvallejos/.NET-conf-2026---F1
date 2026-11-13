using System.Text.Json;

namespace PitWall.Tests.Jev.Real;

public sealed class JevSecretsTests
{
    [Fact]
    public void WebAppSettings_AnyEnvironment_HaveNoJevApiKey()
    {
        var files = Directory.GetFiles(Path.Combine(RepositoryRoot(), "src", "PitWall.Web"), "appsettings*.json");

        Assert.NotEmpty(files);
        Assert.All(files, file =>
        {
            using var settings = JsonDocument.Parse(File.ReadAllText(file));
            var hasKey = settings.RootElement.TryGetProperty("Jev", out var jev) && jev.TryGetProperty("ApiKey", out _);
            Assert.False(hasKey, $"{Path.GetFileName(file)} no debe tener Jev:ApiKey.");
        });
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PitWall.sln")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("No se encontró PitWall.sln.");
    }
}
