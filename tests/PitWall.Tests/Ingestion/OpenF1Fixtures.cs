namespace PitWall.Tests.Ingestion;

/// <summary>Ubica <c>Fixtures/openf1</c> subiendo desde el directorio de salida hasta el proyecto de tests.</summary>
internal static class OpenF1Fixtures
{
    public static string Directory { get; } = Locate();

    private static string Locate()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "Fixtures", "openf1");
            if (System.IO.Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("No se encontró Fixtures/openf1.");
    }
}
