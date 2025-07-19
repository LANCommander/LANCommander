using LANCommander.SDK;
using LANCommander.SDK.Models;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Factories;
using LANCommander.Server.Services.Importers;
using LANCommander.Server.Services.MediaGrabbers;
using LANCommander.Server.Services.ServerEngines;
using Microsoft.Extensions.DependencyInjection;
using Settings = LANCommander.Server.Services.Models.Settings;

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
        services.AddScoped<AuthenticationService>();
        services.AddTransient<SetupService>();

        // Register importers
        services.AddScoped<ImportContextFactory>();
        services.AddScoped<ImportContext>();
        
        services.AddScoped<BaseImporter<SDK.Models.Manifest.Game, Data.Models.Game>, GameImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.Redistributable, Data.Models.Redistributable>, RedistributableImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.Server, Data.Models.Server>, ServerImporter>();
        
        services.AddScoped<BaseImporter<SDK.Models.Manifest.Action, Data.Models.Action>, ActionImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.Archive, Data.Models.Archive>, ArchiveImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.Collection, Data.Models.Collection>, CollectionImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.GameCustomField, Data.Models.GameCustomField>, CustomFieldImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.Company, Data.Models.Company>, PublisherImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.Collection, Data.Models.Collection>, CollectionImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.Engine, Data.Models.Engine>, EngineImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.Genre, Data.Models.Genre>, GenreImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.Key, Data.Models.Key>, KeyImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.Media, Data.Models.Media>, MediaImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.MultiplayerMode, Data.Models.MultiplayerMode>, MultiplayerModeImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.Platform, Data.Models.Platform>, PlatformImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.PlaySession, Data.Models.PlaySession>, PlaySessionImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.Company, Data.Models.Company>, PublisherImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.Save, Data.Models.GameSave>, SaveImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.SavePath, Data.Models.SavePath>, SavePathImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.Script, Data.Models.Script>, ScriptImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.ServerConsole, Data.Models.ServerConsole>, ServerConsoleImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.ServerHttpPath, Data.Models.ServerHttpPath>, ServerHttpPathImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.Tag, Data.Models.Tag>, TagImporter>();
        
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