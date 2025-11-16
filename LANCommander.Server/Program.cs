using Serilog;
using LANCommander.Server.UI;
using LANCommander.Server.Startup;

var builder = WebApplication.CreateBuilder(args);

if (args.Contains("--debugger"))
    builder.WaitForDebugger();

builder.AddAsService();
builder.AddLogger();

builder.AddSettings();

builder.AddRazor();
builder.AddSignalR();
builder.AddCors();
builder.AddControllers();
builder.ConfigureKestrel();
builder.ConfigureAuthentication();
builder.AddIdentity();
builder.AddHangfire();
builder.AddOpenApi();
builder.AddServerProcessStatusMonitor();
builder.AddLANCommanderServices();
builder.AddDatabase(args);

builder.Services.AddHealthChecks();

Log.Debug("Building Application");
var app = builder.Build();

app.UseCors("CorsPolicy");
app.UseHttpsRedirection();

app.MapHealthChecks("/Health");

app.UseMiddlewares();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    Log.Debug("App has been run in a development environment");
    app.UseMigrationsEndPoint();

}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHangfire();

// app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseCookiePolicy();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.UseSignalR();

app.UseStaticFiles();

app.MapScalar();
app.MapEndpoints();

app.MapRazorComponents<App>()
    .DisableAntiforgery()
    .AddInteractiveServerRenderMode();

app.PrepareDirectories();

await app.MigrateDatabaseAsync();
await app.StartServersAsync();
await app.StartBeaconAsync();

app.GenerateThumbnails();

app.Run();

public partial class Program
{
}