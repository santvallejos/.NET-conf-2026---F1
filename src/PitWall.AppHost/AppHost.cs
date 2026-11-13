var builder = DistributedApplication.CreateBuilder(args);

var web = builder.AddProject<Projects.PitWall_Web>("web");

// API key de Jev: parámetro secreto de Aspire (User Secrets "Parameters:typesafe-api-key"), inyectado en Web como
// Jev__ApiKey. Solo se declara si tiene valor: sin key el dashboard no frena el arranque esperando el parámetro y Web
// sigue con Jev:Client = Fake (o, con Real, arranca con Jev deshabilitado y alerta).
const string JevApiKeyParameter = "typesafe-api-key";
if (!string.IsNullOrWhiteSpace(builder.Configuration[$"Parameters:{JevApiKeyParameter}"]))
{
    var jevKey = builder.AddParameter(JevApiKeyParameter, secret: true);
    web.WithEnvironment("Jev__ApiKey", jevKey);
}

builder.Build().Run();
