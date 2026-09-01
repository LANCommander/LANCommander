using System.Reflection;
using LANCommander.SDK;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Interceptors;
using LANCommander.SDK.Models;
using LANCommander.SDK.PowerShell;
using LANCommander.SDK.Services;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Factories;
using LANCommander.Server.Services.Interceptors;
using LANCommander.Server.Services.MediaGrabbers;
using LANCommander.Server.Services.PowerShell;
using LANCommander.HQ.SDK;
using LANCommander.HQ.SDK.Authentication;
using LANCommander.Server.Services.Providers;
using LANCommander.Server.Services.HQ;
using LANCommander.Server.Services.Providers.Metadata;
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
        services.AddScoped<IArchiveClient, ArchiveService>();
        services.AddScoped<StorageLocationService>();
        services.AddScoped<ActionService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<CollectionService>();
        services.AddScoped<GameService>();
        services.AddScoped<LibraryService>();
        services.AddScoped<ScriptService>();
        services.AddScoped<ModuleService>();
        services.AddScoped<GenreService>();
        services.AddScoped<PlatformService>();
        services.AddScoped<KeyService>();
        services.AddScoped<TagService>();
        services.AddScoped<EngineService>();
        services.AddScoped<CompanyService>();
        services.AddScoped<MultiplayerModeService>();
        services.AddScoped<ServerService>();
        services.AddScoped<ServerHttpPathService>();
        services.AddScoped<ServerConsoleService>();
        services.AddScoped<GameSaveService>();
        services.AddScoped<SavePathService>();
        services.AddScoped<PlaySessionService>();
        services.AddScoped<MediaService>();
        services.AddScoped<RedistributableService>();
        services.AddScoped<ConfigToOptionSchemaService>();
        services.AddScoped<ToolService>();
        services.AddSingleton<IHQTokenStore, SettingsHqTokenStore>();
        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<SettingsProvider<Settings.Settings>>();
            var hqSettings = settings.CurrentValue.Server.HQ;

            return new HQClient(new HQClientOptions
            {
                BaseAddress = new Uri(hqSettings.BaseUrl),
                Timeout = TimeSpan.FromSeconds(30),
                TokenStore = sp.GetRequiredService<IHQTokenStore>(),

                ClientName = string.IsNullOrWhiteSpace(hqSettings.ClientName)
                    ? $"LANCommander Server ({Environment.MachineName})"
                    : hqSettings.ClientName,
            });
        });
        services.AddSingleton<IHqAuthApi, HqAuthApi>();
        services.AddSingleton<HqConnectionService>();
        services.AddSingleton<IHqConnectionState>(sp => sp.GetRequiredService<HqConnectionService>());
        services.AddSingleton<HqAuthorizationStateStore>();
        services.AddHostedService<HqConnectionMonitor>();
        services.AddScoped<HqMediaGrabber>();
        services.AddScoped<SteamMediaGrabber>();
        services.AddScoped<SteamGridDBMediaGrabber>();
        services.AddScoped<YouTubeMediaGrabber>();
        services.AddScoped<IMediaGrabberService, CompositeMediaGrabberService>();
        services.AddScoped<MediaToolService>();
        services.AddScoped<UpdateService>();
        services.AddScoped<IssueService>();
        services.AddScoped<PageService>();
        services.AddScoped<UserService>();
        services.AddScoped<RoleService>();
        services.AddScoped<UserCustomFieldService>();
        services.AddScoped<GameCustomFieldService>();
        services.AddScoped<ChatService>();
        services.AddScoped<ChatMessageService>();
        services.AddScoped<ChatThreadService>();
        services.AddScoped<ChatThreadReadStatusService>();
        services.AddScoped<DepotService>();
        services.AddScoped<RatingService>();
        services.AddTransient<SetupService>();
        
        // Register metadata providers
        services.AddScoped<MetadataService>();
        services.AddScoped<IMetadataProvider, HqMetadataProvider>();
        services.AddScoped<IMetadataProvider, IgdbMetadataProvider>();
        services.AddScoped<IMetadataProvider, PcGamingWikiMetadataProvider>();
        services.AddPcGamingWikiClient();
        
        // Register server engines
        services.AddSingleton<IServerEngine, LocalServerEngine>();
        
        services.AddSingleton<DockerServerEngine>();
        services.AddSingleton<IServerEngine>(provider => provider.GetService<DockerServerEngine>());

        services.AddSingleton<RemoteServerEngine>();
        services.AddSingleton<IServerEngine>(provider => provider.GetService<RemoteServerEngine>());

        services.AddSingleton<ServerManager>();

        services.AddSingleton<PlaySessionKeepAliveTracker>();
        services.AddHostedService<PlaySessionSweepService>();

        services.AddSingleton<ScriptDebugger>();
        services.AddSingleton<IScriptDebugger>(sp =>
            sp.GetRequiredService<ScriptDebugger>());
        
        services.AddSingleton<IPXRelayService>();
        services.AddSingleton<IBeaconMessageInterceptor, BeaconMessageInterceptor>();

        services.AddAutoMapper(cfg => { }, typeof(MappingProfile));
        services.AddFusionCache();

        return services;
    }

    /// <summary>
    /// Registers the HTTP client used to talk to PCGamingWiki.
    /// <para>
    /// Their API requires a descriptive User-Agent with contact information (a generic one gets
    /// blocked with a 403) and caps us at 60 requests per minute, where an overrun blocks the
    /// server's IP for a full minute. The throttling handler and the session are singletons so the
    /// request window and the login cookies are shared across the scoped provider instances.
    /// </para>
    /// </summary>
    private static IServiceCollection AddPcGamingWikiClient(this IServiceCollection services)
    {
        services.AddSingleton<PcGamingWikiSession>();
        services.AddSingleton<PcGamingWikiRateLimiter>();

        // The factory owns and disposes the handlers it builds, so this has to be transient. The
        // state that needs to survive a rebuild lives in the singletons above.
        services.AddTransient<PcGamingWikiThrottlingHandler>();

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

        services
            .AddHttpClient(PcGamingWikiMetadataProvider.HttpClientName, client =>
            {
                // The apex domain redirects to www on every request, which would double what we
                // spend against the rate limit.
                client.BaseAddress = new Uri("https://www.pcgamingwiki.com/");
                client.DefaultRequestHeaders.UserAgent.ParseAdd(
                    $"LANCommander/{version} (https://lancommander.app; https://github.com/LANCommander/LANCommander) .NET/{Environment.Version}");
            })
            .ConfigurePrimaryHttpMessageHandler(provider => new HttpClientHandler
            {
                // Handlers get recycled on a timer, so the container has to outlive them or we
                // silently lose the login session.
                CookieContainer = provider.GetRequiredService<PcGamingWikiSession>().Cookies,
                UseCookies = true,
            })
            .AddHttpMessageHandler<PcGamingWikiThrottlingHandler>();

        return services;
    }
}