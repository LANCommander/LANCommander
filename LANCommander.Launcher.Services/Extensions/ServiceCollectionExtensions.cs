using CommandLine;
using LANCommander.Launcher.Data;
using LANCommander.Launcher.Models;
using LANCommander.SDK;
using LANCommander.SDK.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Launcher.Services.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddLANCommander(this IServiceCollection services)
        {
            var settings = SettingService.GetSettings();

            if (settings.Games.InstallDirectories.Length == 0)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    settings.Games.InstallDirectories = new string[] { "C:\\Games" };
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    settings.Games.InstallDirectories = new string[] { "~/Games" };
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    settings.Games.InstallDirectories = new string[] { "~/Games" };

                SettingService.SaveSettings(settings);
            }

            services.AddDbContext<DbContext, DatabaseContext>();

            #region Register Client
            var client = new SDK.Client(settings.Authentication.ServerAddress, settings.Games.InstallDirectories.First());

            client.UseToken(new SDK.Models.AuthToken
            {
                AccessToken = settings.Authentication.AccessToken,
                RefreshToken = settings.Authentication.RefreshToken,
            });

            client.Scripts.Debug = settings.Debug.EnableScriptDebugging;
            client.Scripts.ExternalScriptRunner += Scripts_ExternalScriptRunner;

            services.AddSingleton(client);
            services.AddSingleton<MessageBusService>();
            #endregion

            services.AddScoped<CollectionService>();
            services.AddScoped<CommandLineService>();
            services.AddScoped<CompanyService>();
            services.AddScoped<DownloadService>();
            services.AddScoped<EngineService>();
            services.AddScoped<GameService>();
            services.AddScoped<GenreService>();
            services.AddScoped<ImportService>();
            services.AddScoped<LibraryService>();
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
            if (script.RunAsAdmin)
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

            return false;
        }
    }
}
