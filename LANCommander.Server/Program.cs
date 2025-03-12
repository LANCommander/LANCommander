using System.Diagnostics;
using LANCommander.Server.Data;
using LANCommander.Server.Hubs;
using LANCommander.Server.Services;
using Microsoft.EntityFrameworkCore;
using Hangfire;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Http.Features;
using LANCommander.SDK.Enums;
using Serilog;
using LANCommander.Server;
using LANCommander.Server.Data.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Endpoints;
using LANCommander.Server.Jobs.Background;
using LANCommander.Server.Models;
using LANCommander.Server.Services.Models;
using LANCommander.Server.UI;
using Microsoft.AspNetCore.HttpOverrides;
using Scalar.AspNetCore;
using LANCommander.Server.Services.Importers;
using LANCommander.Server.Startup;

var builder = WebApplication.CreateBuilder(args);

if (args.Contains("--debugger"))
    WaitForDebugger();

if (args.Contains("--docker"))
    SettingService.WorkingDirectory = "/app/config";

builder.AddAsService();
builder.AddLogger();

// Add services to the container.
Log.Debug("Loading settings");

Settings settings;

if (!File.Exists(SettingService.SettingsFile))
{
    var workingDirectory = Path.GetDirectoryName(SettingService.SettingsFile);
    
    if (!String.IsNullOrWhiteSpace(workingDirectory))
        Directory.CreateDirectory(workingDirectory);
    
    settings = new Settings();
    SettingService.SaveSettings(settings);
}
else
    settings = SettingService.GetSettings(true);

Log.Debug("Validating settings");
if (settings.Authentication.TokenSecret.Length < 16)
{
    Log.Debug("JWT token secret is too short. Regenerating...");
    settings.Authentication.TokenSecret = Guid.NewGuid().ToString();
    SettingService.SaveSettings(settings);
}
Log.Debug("Done validating settings");

builder.Services.AddSingleton(settings);

ConfigureDatabaseProvider(settings, args);

builder.AddRazor(settings);
builder.AddSignalR();
builder.AddCors();
builder.AddControllers();
builder.ConfigureKestrel();
builder.AddIdentity(settings);
builder.AddHangfire();
builder.AddOpenApi();
builder.AddLANCommanderServices(settings);
builder.AddDatabase();

Log.Debug("Building Application");
var app = builder.Build();

app.UseCors("CorsPolicy");
app.UseHttpsRedirection();

app.UseMiddleware<RobotsMiddleware>();
app.UseMiddleware<ApiVersioningMiddleware>();
app.UseMiddleware<PingMiddleware>();

app.MapHub<GameServerHub>("/hubs/gameserver");

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

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

app.UseHangfireDashboard();

// app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseCookiePolicy();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.UseMvcWithDefaultRoute();

Log.Debug("Registering Endpoints");

app.MapHub<LoggingHub>("/logging");

app.UseStaticFiles();

app.MapScalar();

app.UseEndpoints(endpoints =>
{
    endpoints.MapDownloadEndpoints();
    endpoints.MapSaveEndpoints();
    endpoints.MapControllers();
    endpoints.MapFallbackToPage("/_Host");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

PrepareDirectories(app);

await EnsureDatabase(app);

await InitializeServerProcesses(app);

BackgroundJob.Enqueue<GenerateThumbnailsJob>(x => x.ExecuteAsync());

app.Run();

static void WaitForDebugger()
{
    var currentProcess = Process.GetCurrentProcess();

    Console.WriteLine($"Waiting for debugger to attach... Process ID: {currentProcess.Id}");

    while (!Debugger.IsAttached)
    {
        Thread.Sleep(100);
    }

    Console.WriteLine("Debugger attached.");
}

static void ConfigureDatabaseProvider(Settings settings, string[] args)
{
    var databaseProviderParameter = args.FirstOrDefault(arg => arg.StartsWith("--database-provider="))?.Split('=', 2).Last();
    var connectionStringParameter = args.FirstOrDefault(arg => arg.StartsWith("--connection-string="))?.Split('=', 2).Last();

    if (!String.IsNullOrWhiteSpace(databaseProviderParameter))
        DatabaseContext.Provider = Enum.Parse<DatabaseProvider>(databaseProviderParameter);
    else
        DatabaseContext.Provider = settings.DatabaseProvider;

    if (!String.IsNullOrWhiteSpace(connectionStringParameter))
        DatabaseContext.ConnectionString = connectionStringParameter;
    else
        DatabaseContext.ConnectionString = settings.DatabaseConnectionString;
}

static void PrepareDirectories(WebApplication app)
{
    var settings = app.Services.GetRequiredService<Settings>();
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogDebug("Ensuring required directories exist");

    IEnumerable<string> directories = [
        settings.Update.StoragePath,
        settings.Launcher.StoragePath,
        settings.Backups.StoragePath,
        "Snippets",
    ];

    foreach (var directory in directories)
    {
        logger.LogDebug("Ensuring directory {Directory} exists", directory);
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);
    }
}

static async Task EnsureDatabase(WebApplication app)
{
    // Migrate
    if (DatabaseContext.Provider != DatabaseProvider.Unknown)
    {
        using var scope = app.Services.CreateAsyncScope();
        using var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var settings = scope.ServiceProvider.GetRequiredService<Settings>();
        logger.LogDebug("Migrating database if required");

        if ((await db.Database.GetPendingMigrationsAsync()).Any())
        {
            if (DatabaseContext.Provider == DatabaseProvider.SQLite)
            {
                var dataSource = new SqliteConnectionStringBuilder(settings.DatabaseConnectionString).DataSource;

                var backupName = Path.Combine("Backups", $"LANCommander.db.{DateTime.Now.ToString("dd-MM-yyyy-HH.mm.ss.bak")}");

                if (File.Exists(dataSource))
                {
                    Log.Information("Migrations pending, database will be backed up to {BackupName}", backupName);
                    File.Copy(dataSource, backupName);
                }
            }
            
            await db.Database.MigrateAsync();
            
            var storageLocationService = scope.ServiceProvider.GetRequiredService<StorageLocationService>();

            var archiveLocation = await storageLocationService.AddMissingAsync(l => l.Type == StorageLocationType.Archive && l.Default, new StorageLocation
            {
                Path = "Uploads",
                Type = StorageLocationType.Archive,
                Default = true,
            });
            
            if (!Directory.Exists(archiveLocation.Value.Path))
                Directory.CreateDirectory(archiveLocation.Value.Path);
            
            var mediaLocation = await storageLocationService.AddMissingAsync(l => l.Type == StorageLocationType.Media && l.Default, new StorageLocation
            {
                Path = "Media",
                Type = StorageLocationType.Media,
                Default = true,
            });
            
            if (!Directory.Exists(mediaLocation.Value.Path))
                Directory.CreateDirectory(mediaLocation.Value.Path);
            
            var saveLocation = await storageLocationService.AddMissingAsync(l => l.Type == StorageLocationType.Save && l.Default, new StorageLocation
            {
                Path = "Saves",
                Type = StorageLocationType.Save,
                Default = true,
            });
            
            if (!Directory.Exists(saveLocation.Value.Path))
                Directory.CreateDirectory(saveLocation.Value.Path);
        }
        else
            logger.LogDebug("No pending migrations are available. Skipping database migration.");
    }
}

static async Task InitializeServerProcesses(WebApplication app)
{
    if (DatabaseContext.Provider != DatabaseProvider.Unknown)
    {
        // Autostart any server processes
        using var scope = app.Services.CreateScope();
        var serverService = scope.ServiceProvider.GetRequiredService<ServerService>();
        var serverProcessService = scope.ServiceProvider.GetRequiredService<ServerProcessService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogDebug("Autostarting Servers");
        
        // Autostart IPX relay
        scope.ServiceProvider.GetService<IPXRelayService>();

        foreach (var server in await serverService.GetAsync(s => s.Autostart && s.AutostartMethod == ServerAutostartMethod.OnApplicationStart))
        {
            try
            {
                logger.LogDebug("Autostarting server {ServerName} with a delay of {AutostartDelay} seconds", server.Name, server.AutostartDelay);

                Task.Run(() =>
                {
                    if (server.Autostart && server.AutostartDelay > 0)
                        Task.Delay(TimeSpan.FromSeconds(server.AutostartDelay)).Wait();
                    
                    return serverProcessService.StartServerAsync(server.Id);
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An unexpected error occurred while trying to autostart the server {ServerName}", server.Name);
            }
        }
    }
}