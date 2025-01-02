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
using LANCommander.Server.Models;
using LANCommander.Server.Endpoints;
using System.IO.Abstractions;

var builder = WebApplication.CreateBuilder(args);

builder.AddLogger();

// Add services to the container.
Log.Debug("Loading settings");
LANCommanderSettings settings = SettingService.GetSettings(true);
builder.Services.AddSingleton(settings);
Log.Debug("Validating settings");
if (settings.Authentication.TokenSecret.Length < 16)
{
    Log.Debug("JWT token secret is too short. Regenerating...");
    settings.Authentication.TokenSecret = Guid.NewGuid().ToString();
    SettingService.SaveSettings(settings);
}
Log.Debug("Done validating settings");

builder.AddRazor();
builder.AddSignalR();
builder.AddCors();
builder.AddControllers();

builder.Services.AddAutoMapper(typeof(LANCommanderMappingProfile));

builder.WebHost.ConfigureKestrel((ctx, options) =>
{
    var settings = options.ApplicationServices.GetRequiredService<LANCommanderSettings>();
    var logger = options.ApplicationServices.GetRequiredService<ILogger<Program>>();
    logger.LogDebug("Starting web server on port {Port}", settings.Port);
    // Configure as HTTP only
    options.ListenAnyIP(settings.Port);

    options.Limits.MaxRequestBodySize = long.MaxValue;
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5);
}).UseKestrel();

builder.AddIdentity(settings);

builder.AddHangfire();

builder.Services.AddFusionCache();

Log.Debug("Registering Swashbuckle");
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

Log.Debug("Registering AntDesign Blazor");
builder.Services.AddAntDesign();

builder.Services.AddHttpClient();

builder.WebHost.UseStaticWebAssets();

builder.AddLANCommanderServices(settings);
builder.AddDatabase();

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = long.MaxValue;
});

builder.Services.AddSingleton<IFileSystem>(new FileSystem());
builder.Services.AddSingleton(sp =>
{
    var fs = sp.GetRequiredService<IFileSystem>();
    return fs.Directory;
});
builder.Services.AddSingleton(sp =>
{
    var fs = sp.GetRequiredService<IFileSystem>();
    return fs.File;
});
builder.Services.AddSingleton(sp =>
{
    var fs = sp.GetRequiredService<IFileSystem>();
    return fs.Path;
});

Log.Debug("Building Application");
var app = builder.Build();

app.UseCors("CorsPolicy");

app.MapHub<GameServerHub>("/hubs/gameserver");

app.UseRobots();
app.UseApiVersioning();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    Log.Debug("App has been run in a development environment");
    app.UseMigrationsEndPoint();
    app.UseSwagger();
    app.UseSwaggerUI();
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

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapDownloadEndpoints();

app.UseMvcWithDefaultRoute();

Log.Debug("Registering Endpoints");

app.MapHub<LoggingHub>("/logging");

app.UseEndpoints(endpoints =>
{
    endpoints.MapBlazorHub();
    endpoints.MapFallbackToPage("/_Host");
    endpoints.MapControllers();
});

PrepareDirectories(app);

await EnsureDatabase(app);

await InitializeServerProcesses(app);

app.Run();

static void PrepareDirectories(WebApplication app)
{
    var settings = app.Services.GetRequiredService<LANCommanderSettings>();
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogDebug("Ensuring required directories exist");

    IEnumerable<string> directories = [
        settings.Logs.StoragePath,
        settings.Archives.StoragePath,
        settings.UserSaves.StoragePath,
        settings.Media.StoragePath,
        settings.Update.StoragePath,
        "Snippets",
        "Backups"
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
    using var scope = app.Services.CreateAsyncScope();
    using var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var settings = scope.ServiceProvider.GetRequiredService<LANCommanderSettings>();
    logger.LogDebug("Migrating database if required");

    if (!(await db.Database.GetPendingMigrationsAsync()).Any())
    {
        logger.LogDebug("No pending migrations are available. Skipping database migration.");
        return;
    }

    var dataSource = new SqliteConnectionStringBuilder(settings.DatabaseConnectionString).DataSource;

    var backupName = Path.Combine("Backups", $"LANCommander.db.{DateTime.Now:dd-MM-yyyy-HH.mm.ss.bak}");

    if (File.Exists(dataSource))
    {
        logger.LogInformation("Migrations pending, database will be backed up to {BackupName}", backupName);
        File.Copy(dataSource, backupName);
    }

    await db.Database.MigrateAsync();
}

static async Task InitializeServerProcesses(WebApplication app)
{
    // Autostart any server processes
    using var scope = app.Services.CreateScope();
    var serverService = scope.ServiceProvider.GetRequiredService<ServerService>();
    var serverProcessService = scope.ServiceProvider.GetRequiredService<ServerProcessService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogDebug("Autostarting Servers");

    foreach (var server in await serverService.Get(s => s.Autostart && s.AutostartMethod == ServerAutostartMethod.OnApplicationStart).ToListAsync())
    {
        try
        {
            logger.LogDebug("Autostarting server {ServerName} with a delay of {AutostartDelay} seconds", server.Name, server.AutostartDelay);

            if (server.AutostartDelay > 0)
                await Task.Delay(server.AutostartDelay);

            await serverProcessService.StartServerAsync(server.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unexpected error occurred while trying to autostart the server {ServerName}", server.Name);
        }
    }
}