using LANCommander.SDK;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Factories;
using LANCommander.Server.Services.Importers;
using LANCommander.Server.Services.MediaGrabbers;
using LANCommander.Server.Services.Models;
using Serilog;

namespace LANCommander.Server.Startup;

public static class Services
{
    public static WebApplicationBuilder AddLANCommanderServices(this WebApplicationBuilder builder, Settings settings)
    {
        Log.Debug("Registering services");
        builder.Services.AddSingleton(new Client("", ""));
        builder.Services.AddScoped<IdentityContextFactory>();
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
        builder.Services.AddScoped<RoleService>();
        builder.Services.AddScoped<UserCustomFieldService>();
        builder.Services.AddScoped<AuthenticationService>();
        builder.Services.AddTransient<SetupService>();
        builder.Services.AddScoped(typeof(ImportService<>));

        // Register importers
        builder.Services.AddScoped<IImporter<Data.Models.Game>, GameImporter>();
        builder.Services.AddScoped<IImporter<Data.Models.Server>, ServerImporter>();
        builder.Services.AddScoped<IImporter<Data.Models.Redistributable>, RedistributableImporter>();

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

        return builder;
    }
}