using LANCommander.SDK;
using LANCommander.SDK.Interceptors;
using LANCommander.SDK.Models;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Factories;
using LANCommander.Server.Services.Interceptors;
using LANCommander.Server.Services.MediaGrabbers;
using LANCommander.Server.Services.ServerEngines;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Extensions;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddLANCommanderServer(this IServiceCollection services)
    {
        services.AddScoped<IGitHubService, GitHubService>();
        services.AddScoped<IdentityContextFactory>();
        services.AddScoped<AuthenticationService>();
        services.AddScoped<ArchiveService>();
        services.AddScoped<StorageLocationService>();
        services.AddScoped<ActionService>();
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
        services.AddScoped<MultiplayerModeService>();
        services.AddScoped<IGDBService>();
        services.AddScoped<ServerService>();
        services.AddScoped<ServerHttpPathService>();
        services.AddScoped<ServerConsoleService>();
        services.AddScoped<GameSaveService>();
        services.AddScoped<SavePathService>();
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
        services.AddScoped<GameCustomFieldService>();
        services.AddScoped<SteamCMDService>();
        services.AddScoped<ChatService>();
        services.AddScoped<ChatMessageService>();
        services.AddScoped<ChatThreadService>();
        services.AddScoped<ChatThreadReadStatusService>();
        services.AddTransient<SetupService>();
        
        // Register server engines
        services.AddSingleton<IServerEngine, LocalServerEngine>();
        
        services.AddSingleton<DockerServerEngine>();
        services.AddSingleton<IServerEngine>(provider => provider.GetService<DockerServerEngine>());
        
        services.AddSingleton<IPXRelayService>();
        services.AddSingleton<IBeaconMessageInterceptor, BeaconMessageInterceptor>();

        services.AddAutoMapper(cfg => { }, typeof(MappingProfile));
        services.AddFusionCache();

        return services;
    }
}