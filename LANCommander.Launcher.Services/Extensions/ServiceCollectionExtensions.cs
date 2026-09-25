using LANCommander.Launcher.Data;
using LANCommander.Launcher.Services.Import;
using LANCommander.Launcher.Services.Import.Factories;
using LANCommander.Launcher.Services.Import.Importers;
using LANCommander.Launcher.Services.Packaging;
using LANCommander.Launcher.Services.ScriptDebugging;
using LANCommander.SDK.PowerShell.Debugging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LANCommander.SDK.PowerShell;

namespace LANCommander.Launcher.Services.Extensions
{
    public class LANCommanderOptions
    {
        public ILogger? Logger { get; set; }
        public string? ServerAddress { get; set; }
    }

    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddLANCommanderLauncher(this IServiceCollection services, Action<LANCommanderOptions>? configure = null)
        {
            services.AddDbContext<DbContext, DatabaseContext>(options =>
            {
                options.EnableSensitiveDataLogging();
            });

            #region Register Client
            var options = new LANCommanderOptions();

            configure?.Invoke(options);

            services.AddSingleton<MessageBusService>();
            services.AddSingleton<AuthenticationService>();
            services.AddSingleton<KeepAliveService>();
            #endregion

            services.AddSingleton<ICurrentProcessInfo, CurrentProcessInfo>();
            services.AddSingleton<IElevatedProcessLauncher, ElevatedProcessLauncher>();

            #region Packaging
            // Singleton because a capture session outlives any DI scope, and because several
            // workers report into one merged change set. Selected by platform so view models
            // can bind to IsSupported instead of branching on the OS.
            if (OperatingSystem.IsWindows())
            {
                services.AddSingleton<IPackagingWorkerFactory, PackagingWorkerFactory>();
                services.AddSingleton<IPackagingSessionService, PackagingSessionService>();
            }
            else
            {
                services.AddSingleton<IPackagingSessionService, UnsupportedPackagingSessionService>();
            }
            #endregion

            services.AddSingleton<IScriptInterceptor, ElevatedScriptInterceptor>();

            // Script debugger windows register here; scripts for a game whose window is open attach to it.
            // An elevated child process replaces this with a broker that talks to the launcher over a pipe.
            services.AddSingleton<ScriptDebugBroker>();
            services.AddSingleton<IScriptDebugBroker>(sp => sp.GetRequiredService<ScriptDebugBroker>());
            services.AddScoped<ScriptDebugWorkspaceService>();
            services.AddScoped<ScriptDebugRunner>();

            services.AddSingleton<ImportManagerService>();
            services.AddScoped<CollectionService>();
            services.AddScoped<CommandLineService>();
            services.AddScoped<CompanyService>();
            services.AddScoped<InstallService>();
            services.AddScoped<EngineService>();
            services.AddScoped<GameService>();
            services.AddScoped<GenreService>();
            services.AddScoped<ImportService>();
            services.AddScoped<LibraryService>();
            services.AddScoped<UserService>();
            services.AddScoped<DepotService>();
            services.AddScoped<MediaService>();
            services.AddScoped<MultiplayerModeService>();
            services.AddScoped<PlatformService>();
            services.AddScoped<PlaySessionService>();
            services.AddScoped<ProfileService>();
            services.AddScoped<RedistributableService>();
            services.AddScoped<ToolService>();
            services.AddScoped<SaveService>();
            services.AddScoped<TagService>();
            services.AddScoped<UpdateService>();
            
            #region Import

            services.AddScoped<ImportContextFactory>();
            
            services.AddScoped<CollectionImporter>();
            services.AddScoped<DeveloperImporter>();
            services.AddScoped<EngineImporter>();
            services.AddScoped<GameImporter>();
            services.AddScoped<GenreImporter>();
            services.AddScoped<MultiplayerModeImporter>();
            services.AddScoped<PlatformImporter>();
            services.AddScoped<PublisherImporter>();
            services.AddScoped<TagImporter>();
            services.AddScoped<MediaImporter>();
            services.AddScoped<ToolImporter>();
            #endregion

            return services;
        }
    }
}
