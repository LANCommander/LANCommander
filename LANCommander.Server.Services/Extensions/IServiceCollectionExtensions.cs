using LANCommander.SDK;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Factories;
using LANCommander.Server.Services.Importers;
using LANCommander.Server.Services.MediaGrabbers;
using LANCommander.Server.Services.Models;
using LANCommander.Server.Services.ServerEngines;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Extensions;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddLANCommanderServer(this IServiceCollection services, Settings settings)
    {
        services.AddSingleton(new Client("", ""));
        services.AddScoped<IGitHubService, GitHubService>();
        services.AddScoped<IdentityContextFactory>();
        services.AddScoped<SettingService>();
        services.AddScoped<ArchiveService>();
        services.AddScoped<StorageLocationService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<CollectionService>();
        services.AddScoped<GameService>();
        services.AddScoped<LibraryService>();
        services.AddScoped<ScriptService>();
        services.AddScoped<GenreService>();
        services.AddScoped<PlatformService>();
        services.AddScoped<KeyService>();
        services.AddScoped<TagService>();
        services.AddScoped<EngineService>();
        services.AddScoped<CompanyService>();
        services.AddScoped<IGDBService>();
        services.AddScoped<ServerService>();
        services.AddScoped<ServerConsoleService>();
        services.AddScoped<GameSaveService>();
        services.AddScoped<PlaySessionService>();
        services.AddScoped<MediaService>();
        services.AddScoped<RedistributableService>();
        services.AddScoped<IMediaGrabberService, SteamGridDBMediaGrabber>();
        services.AddScoped<UpdateService>();
        services.AddScoped<IssueService>();
        services.AddScoped<PageService>();
        services.AddScoped<UserService>();
        services.AddScoped<RoleService>();
        services.AddScoped<UserCustomFieldService>();
        services.AddScoped<AuthenticationService>();
        services.AddTransient<SetupService>();
        services.AddScoped(typeof(ImportService<>));

        // Register importers
        services.AddScoped<ImportContext<Data.Models.Game>>();
        services.AddScoped<ImportContext<Data.Models.Redistributable>>();
        services.AddScoped<ImportContext<Data.Models.Server>>();
        
        // Register server engines
        services.AddSingleton<IServerEngine, LocalServerEngine>();
        
        services.AddSingleton<DockerServerEngine>();
        services.AddSingleton<IServerEngine>(provider => provider.GetService<DockerServerEngine>());
        
        services.AddSingleton<IPXRelayService>();
        
        services.AddAutoMapper(typeof(MappingProfile));
        services.AddFusionCache();

        if (settings.Beacon.Enabled)
            services.AddHostedService<BeaconService>();

        return services;
    }
}