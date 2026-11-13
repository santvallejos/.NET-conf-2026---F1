using System.Text;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using PitWall.Ingestion;

IngestionOptions options;
try
{
    options = IngestionArguments.Parse(args);
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine(exception.Message);
    Console.Error.WriteLine(IngestionArguments.Usage);
    return 2;
}

Console.OutputEncoding = Encoding.UTF8;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

var databaseDirectory = Path.GetDirectoryName(Path.GetFullPath(options.DatabasePath));
if (!string.IsNullOrEmpty(databaseDirectory))
{
    Directory.CreateDirectory(databaseDirectory);
}

await using var provider = new ServiceCollection().AddPitWallIngestion(options).BuildServiceProvider();
var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("PitWall.Ingestion");

try
{
    var report = await provider.GetRequiredService<SessionIngestor>().IngestAsync(options, cancellation.Token);
    logger.LogInformation("Ingesta terminada: {Report}", report);
    return 0;
}
catch (OpenF1IngestionException exception)
{
    logger.LogError(exception, "Falló la ingesta de la sesión {SessionKey}.", options.SessionKey);
    return 1;
}
catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
{
    logger.LogWarning("Ingesta de la sesión {SessionKey} cancelada; la base queda como estaba.", options.SessionKey);
    return 130;
}
