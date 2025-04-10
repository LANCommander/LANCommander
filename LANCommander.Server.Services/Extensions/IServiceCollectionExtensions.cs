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
        services.AddScoped(typeof(ImportService));

        // Register importers
        services.AddScoped<ImportContext<Data.Models.Game>>();
        services.AddScoped<ImportContext<Data.Models.Redistributable>>();
        services.AddScoped<ImportContext<Data.Models.Server>>();
        
        services.AddScoped<IImporter<SDK.Models.Manifest.Game, Data.Models.Game>, GameImporter>();
        services.AddScoped<IImporter<SDK.Models.Manifest.Redistributable, Data.Models.Redistributable>, RedistributableImporter>();
        services.AddScoped<IImporter<SDK.Models.Manifest.Server, Data.Models.Server>, ServerImporter>();
        
        services.AddScoped<IImporter<SDK.Models.Manifest.Action, Data.Models.Action>, ActionImporter<Data.Models.Action>>();
        services.AddScoped<IImporter<SDK.Models.Manifest.Archive, Data.Models.Archive>, ArchiveImporter<Data.Models.Archive>>();
        services.AddScoped<IImporter<SDK.Models.Manifest.Collection, Data.Models.Collection>, CollectionImporter<Data.Models.Collection>>();
        services.AddScoped<IImporter<SDK.Models.Manifest.GameCustomField, Data.Models.GameCustomField>, CustomFieldImporter<Data.Models.GameCustomField>>();
        services.AddScoped<IImporter<SDK.Models.Manifest.Company, Data.Models.Company>, PublisherImporter<Data.Models.Company>>();
        services.AddScoped<IImporter<SDK.Models.Manifest.Collection, Data.Models.Collection>, CollectionImporter<Data.Models.Collection>>();
        services.AddScoped<IImporter<SDK.Models.Manifest.Engine, Data.Models.Engine>, EngineImporter<Data.Models.Engine>>();
        services.AddScoped<IImporter<SDK.Models.Manifest.Genre, Data.Models.Genre>, GenreImporter<Data.Models.Genre>>();
        services.AddScoped<IImporter<SDK.Models.Manifest.Key, Data.Models.Key>, KeyImporter<Data.Models.Key>>();
        services.AddScoped<IImporter<SDK.Models.Manifest.Media, Data.Models.Media>, MediaImporter<Data.Models.Media>>();
        services.AddScoped<IImporter<SDK.Models.Manifest.MultiplayerMode, Data.Models.MultiplayerMode>, MultiplayerModeImporter<Data.Models.MultiplayerMode>>();
        services.AddScoped<IImporter<SDK.Models.Manifest.Platform, Data.Models.Platform>, PlatformImporter<Data.Models.Platform>>();
        services.AddScoped<IImporter<SDK.Models.Manifest.PlaySession, Data.Models.PlaySession>, PlaySessionImporter<Data.Models.PlaySession>>();
        services.AddScoped<IImporter<SDK.Models.Manifest.Company, Data.Models.Company>, PublisherImporter<Data.Models.Company>>();
        services.AddScoped<IImporter<SDK.Models.Manifest.Save, Data.Models.GameSave>, SaveImporter<Data.Models.GameSave>>();
        services.AddScoped<IImporter<SDK.Models.Manifest.SavePath, Data.Models.SavePath>, SavePathImporter<Data.Models.SavePath>>();
        services.AddScoped<IImporter<SDK.Models.Manifest.Script, Data.Models.Script>, ScriptImporter<Data.Models.Script>>();
        services.AddScoped<IImporter<SDK.Models.Manifest.ServerConsole, Data.Models.ServerConsole>, ServerConsoleImporter<Data.Models.ServerConsole>>();
        services.AddScoped<IImporter<SDK.Models.Manifest.ServerHttpPath, Data.Models.ServerHttpPath>, ServerHttpPathImporter<Data.Models.ServerHttpPath>>();
        services.AddScoped<IImporter<SDK.Models.Manifest.Tag, Data.Models.Tag>, TagImporter<Data.Models.Tag>>();
        
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