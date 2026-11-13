namespace PitWall.Tests.Jev;

/// <summary>Lee fixtures de <c>Fixtures/jev</c> subiendo desde el directorio de salida hasta el proyecto de tests.</summary>
internal static class JevFixtures
{
    private static readonly string[] FixtureFolder = ["Fixtures", "jev"];

    public static string Read(string fileName)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine([current.FullName, .. FixtureFolder, fileName]);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            current = current.Parent;
        }

        throw new FileNotFoundException($"No se encontró el fixture {fileName}.", fileName);
    }
}
