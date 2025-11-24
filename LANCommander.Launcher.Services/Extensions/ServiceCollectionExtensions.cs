using CommandLine;
using LANCommander.Launcher.Data;
using LANCommander.Launcher.Models;
using LANCommander.SDK;
using LANCommander.SDK.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using LANCommander.SDK.PowerShell;
using LANCommander.SDK.Providers;

namespace LANCommander.Launcher.Services.Extensions
{
    public class LANCommanderOptions
    {
        public ILogger Logger { get; set; }
        public string ServerAddress { get; set; }
    }

    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddLANCommanderLauncher(this IServiceCollection services, Action<LANCommanderOptions> configure)
        {
            services.AddDbContext<DbContext, DatabaseContext>();

            #region Register Client
            var options = new LANCommanderOptions();

            configure(options);
            
            services.AddSingleton<MessageBusService>();
            services.AddSingleton<AuthenticationService>();
            services.AddSingleton<KeepAliveService>();
            #endregion

            services.AddSingleton<IExternalScriptRunner, ExternalScriptRunner>();
            
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
            services.AddScoped<SaveService>();
            services.AddScoped<TagService>();
            services.AddScoped<UpdateService>();

            return services;
        }
    }
}
