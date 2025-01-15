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

namespace LANCommander.Launcher.Services.Extensions
{
    public class LANCommanderOptions
    {
        public ILogger Logger { get; set; }
        public string ServerAddress { get; set; }
    }

    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddLANCommander(this IServiceCollection services, Action<LANCommanderOptions> configure)
        {
            var settings = SettingService.GetSettings();

            if (settings.Games.InstallDirectories.Length == 0)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    settings.Games.InstallDirectories = new string[] { "C:\\Games" };
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    settings.Games.InstallDirectories = new string[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Games") };
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    settings.Games.InstallDirectories = new string[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Games") };

                SettingService.SaveSettings(settings);
            }

            services.AddDbContext<DbContext, DatabaseContext>();

            #region Register Client
            var options = new LANCommanderOptions();

            configure(options);

            var client = new SDK.Client(options.ServerAddress, settings.Games.InstallDirectories.First(), options.Logger);

            client.Scripts.Debug = settings.Debug.EnableScriptDebugging;
            client.Scripts.ExternalScriptRunner += Scripts_ExternalScriptRunner;

            services.AddSingleton(client);
            services.AddSingleton<MessageBusService>();
            #endregion

            services.AddScoped<AuthenticationService>();
            services.AddScoped<CollectionService>();
            services.AddScoped<CommandLineService>();
            services.AddScoped<CompanyService>();
            services.AddScoped<InstallService>();
            services.AddScoped<EngineService>();
            services.AddScoped<GameService>();
            services.AddScoped<GenreService>();
            services.AddScoped<ImportService>();
            services.AddScoped<LibraryService>();
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

        private static async Task<bool> Scripts_ExternalScriptRunner(SDK.PowerShell.PowerShellScript script)
        {
            try
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);

                var isElevated = principal.IsInRole(WindowsBuiltInRole.Administrator);

                if (script.RunAsAdmin && !isElevated)
                {
                    var manifest = script.Variables.GetValue<GameManifest>("GameManifest");

                    var options = new RunScriptCommandLineOptions
                    {
                        InstallDirectory = script.Variables.GetValue<string>("InstallDirectory"),
                        GameId = manifest.Id,
                        Type = script.Type
                    };

                    if (script.Type == ScriptType.KeyChange)
                        options.AllocatedKey = script.Variables.GetValue<string>("AllocatedKey");

                    if (script.Type == ScriptType.NameChange)
                    {
                        options.OldPlayerAlias = script.Variables.GetValue<string>("OldPlayerAlias");
                        options.NewPlayerAlias = script.Variables.GetValue<string>("NewPlayerAlias");
                    }

                    var arguments = Parser.Default.FormatCommandLine(options);

                    var path = Process.GetCurrentProcess().MainModule.FileName;

                    var process = new Process();

                    process.StartInfo.FileName = path;
                    process.StartInfo.Verb = "runas";
                    process.StartInfo.UseShellExecute = true;
                    process.StartInfo.WorkingDirectory = script.WorkingDirectory;
                    process.StartInfo.Arguments = arguments;

                    process.Start();

                    await process.WaitForExitAsync();

                    return true;
                }
            }
            catch (Exception ex)
            {
                // Not running as admin
            }

            return false;
        }
    }
}
