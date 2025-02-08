using LANCommander.SDK;
using LANCommander.Server.Models;
using LANCommander.Server.Services.MediaGrabbers;
using LANCommander.Server.Services;
using Serilog;
using Hangfire;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Interceptors;
using LANCommander.Server.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Server;

public static class ServiceExtensions
{
    public static void AddLANCommanderServices(this WebApplicationBuilder builder, Settings settings)
    {
        Log.Debug("Registering services");
        builder.Services.AddSingleton(new Client("", ""));
        builder.Services.AddScoped<SettingService>();
        builder.Services.AddScoped<ArchiveService>();
        builder.Services.AddScoped<StorageLocationService>();
        builder.Services.AddScoped<CategoryService>();
        builder.Services.AddScoped<CollectionService>();
        builder.Services.AddScoped<GameService>();
        builder.Services.AddScoped<LibraryService>();
        builder.Services.AddScoped<ScriptService>();
        builder.Services.AddScoped<GenreService>();
        builder.Services.AddScoped<PlatformService>();
        builder.Services.AddScoped<KeyService>();
        builder.Services.AddScoped<TagService>();
        builder.Services.AddScoped<EngineService>();
        builder.Services.AddScoped<CompanyService>();
        builder.Services.AddScoped<IGDBService>();
        builder.Services.AddScoped<ServerService>();
        builder.Services.AddScoped<ServerConsoleService>();
        builder.Services.AddScoped<GameSaveService>();
        builder.Services.AddScoped<PlaySessionService>();
        builder.Services.AddScoped<MediaService>();
        builder.Services.AddScoped<RedistributableService>();
        builder.Services.AddScoped<IMediaGrabberService, SteamGridDBMediaGrabber>();
        builder.Services.AddScoped<UpdateService>();
        builder.Services.AddScoped<IssueService>();
        builder.Services.AddScoped<PageService>();
        builder.Services.AddScoped<UserService>();
        builder.Services.AddScoped<UserCustomFieldService>();
        builder.Services.AddScoped<AuthenticationService>();
        builder.Services.AddScoped<SetupService>();

        builder.Services.AddSingleton<ServerProcessService>();
        builder.Services.AddSingleton<IPXRelayService>();
        
        builder.Services.AddAutoMapper(typeof(LANCommanderMappingProfile));
        builder.Services.AddFusionCache();
        builder.Services.AddAntDesign();
        builder.Services.AddHttpClient();
        builder.Services.AddHttpContextAccessor();

        if (settings.Beacon.Enabled)
        {
            Log.Debug("The beacons have been lit! LANCommander calls for players!");
            builder.Services.AddHostedService<BeaconService>();
        }
    }

    public static void AddAsService(this WebApplicationBuilder builder)
    {
        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = "LANCommander Server";
        });

        builder.Services.AddSystemd();
    }

    public static void AddHangfire(this WebApplicationBuilder builder)
    {
        builder.Services.AddHangfire(static (sp, configuration) =>
        {
            var logger = sp.GetRequiredService<ILogger<Program>>();
            logger.LogDebug("Initializing Hangfire");
            configuration
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseInMemoryStorage();
        });
        builder.Services.AddHangfireServer();
    }

    public static void AddDatabase(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<AuditingInterceptor>();
        
        builder.Services.AddDbContextFactory<DatabaseContext>();
        builder.Services.AddDbContext<DatabaseContext>();
        builder.Services.AddDatabaseDeveloperPageExceptionFilter();
    }
}
