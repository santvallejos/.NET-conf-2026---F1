using PitWall.Jev;
using PitWall.Jev.Guards;
using PitWall.Replay;
using PitWall.Web.Components;
using PitWall.Web.Components.Panels.Energy;
using PitWall.Web.Components.Panels.Tyres;
using PitWall.Web.Hubs;
using PitWall.Web.Services;
using PitWall.Web.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddPitWallStorage(builder.Configuration, builder.Environment);
builder.Services.AddPitWallReplay(builder.Configuration);
builder.Services.AddPitWallJev();
builder.Services.AddPitWallUi(builder.Configuration);
builder.Services.AddPitWallTyres(builder.Configuration);
builder.Services.AddPitWallEnergy(builder.Configuration);
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
app.MapHub<PitWallHub>(PitWallHub.Route);

app.MapDefaultEndpoints();

app.Run();
