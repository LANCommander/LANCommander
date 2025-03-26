using LANCommander.Server.Services;
using Serilog;
using LANCommander.Server.Services.Models;
using LANCommander.Server.UI;
using LANCommander.Server.Startup;

var builder = WebApplication.CreateBuilder(args);

if (args.Contains("--debugger"))
    builder.WaitForDebugger();

if (args.Contains("--docker"))
    SettingService.WorkingDirectory = "/app/config";

builder.AddAsService();
builder.AddLogger();

Settings settings;

builder.AddSettings(out settings);

builder.AddRazor();
builder.AddSignalR();
builder.AddCors();
builder.AddControllers();
builder.ConfigureKestrel();
builder.ConfigureAuthentication(settings);
builder.AddIdentity(settings);
builder.AddHangfire();
builder.AddOpenApi();
builder.AddServerProcessStatusMonitor();
builder.AddLANCommanderServices(settings);
builder.AddDatabase(settings, args);

Log.Debug("Building Application");
var app = builder.Build();

app.UseCors("CorsPolicy");
app.UseHttpsRedirection();

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
app.UseAntiforgery();

app.UseSignalR();

app.UseStaticFiles();

app.MapScalar();
app.MapEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.PrepareDirectories();

await app.MigrateDatabaseAsync();
await app.StartServersAsync();

app.GenerateThumbnails();

app.Run();

public partial class Program
{
}