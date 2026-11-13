using PitWall.Jev;
using PitWall.Jev.Guards;
using PitWall.Web.Components;
using PitWall.Web.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddPitWallStorage(builder.Configuration, builder.Environment);
builder.Services.AddPitWallJev();
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics.AddMeter(JevMetrics.MeterName));

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
