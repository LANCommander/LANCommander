using CommandLine;
using LANCommander.Launcher.Models;
using LANCommander.SDK.Helpers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Launcher.Services
{
    public class CommandLineService : BaseService
    {
        private readonly GameService GameService;
        private readonly InstallService InstallService;
        private readonly ImportService ImportService;
        private readonly ProfileService ProfileService;

        private Settings Settings = SettingService.GetSettings();

        public CommandLineService(
            SDK.Client client,
            ILogger<CommandLineService> logger,
            GameService gameService,
            InstallService installService,
            ImportService importService,
            ProfileService profileService) : base(client, logger)
        {
            GameService = gameService;
            InstallService = installService;
            ImportService = importService;
            ProfileService = profileService;
        }

        public async Task ParseCommandLineAsync(string[] args)
        {
            await Client.ValidateTokenAsync();

                
            var result = Parser.Default.ParseArguments
                <
                    RunScriptCommandLineOptions,
                    InstallCommandLineOptions,
                    UninstallCommandLineOptions,
                    SyncCommandLineOptions,
                    ImportCommandLineOptions,
                    ExportCommandLineOptions,
                    UploadCommandLineOptions,
                    LoginCommandLineOptions,
                    LogoutCommandLineOptions,
                    ChangeAliasCommandLineOptions
                >(args);

            await result.WithParsedAsync<RunScriptCommandLineOptions>(RunScript);
            await result.WithParsedAsync<InstallCommandLineOptions>(Install);
            await result.WithParsedAsync<UninstallCommandLineOptions>(Uninstall);
            await result.WithParsedAsync<RunCommandLineOptions>(Run);
            await result.WithParsedAsync<SyncCommandLineOptions>(Sync);
            await result.WithParsedAsync<ImportCommandLineOptions>(Import);
            await result.WithParsedAsync<ExportCommandLineOptions>(Export);
            await result.WithParsedAsync<UploadCommandLineOptions>(Upload);
            await result.WithParsedAsync<LoginCommandLineOptions>(Login);
            await result.WithParsedAsync<LogoutCommandLineOptions>(Logout);
            await result.WithParsedAsync<ChangeAliasCommandLineOptions>(ChangeAlias);
        }

        private async Task RunScript(RunScriptCommandLineOptions options)
        {
            switch (options.Type)
            {
                case SDK.Enums.ScriptType.Install:
                    await Client.Scripts.RunInstallScriptAsync(options.InstallDirectory, options.GameId);
                    break;

                case SDK.Enums.ScriptType.Uninstall:
                    await Client.Scripts.RunUninstallScriptAsync(options.InstallDirectory, options.GameId);
                    break;

                case SDK.Enums.ScriptType.BeforeStart:
                    await Client.Scripts.RunBeforeStartScriptAsync(options.InstallDirectory, options.GameId);
                    break;

                case SDK.Enums.ScriptType.AfterStop:
                    await Client.Scripts.RunAfterStopScriptAsync(options.InstallDirectory, options.GameId);
                    break;

                case SDK.Enums.ScriptType.NameChange:
                    await Client.Scripts.RunNameChangeScriptAsync(options.InstallDirectory, options.GameId, options.NewPlayerAlias);
                    break;

                case SDK.Enums.ScriptType.KeyChange:
                    await Client.Scripts.RunKeyChangeScriptAsync(options.InstallDirectory, options.GameId, options.AllocatedKey);
                    break;
            }
        }

        private async Task Install(InstallCommandLineOptions options)
        {
            Logger.LogInformation($"Downloading and installing game with ID {options.GameId}...");

            try
            {
                var game = await GameService.GetAsync(options.GameId);

                await InstallService.Add(game, options.InstallDirectory);
                await InstallService.Next();

                game = await GameService.GetAsync(options.GameId);

                Logger.LogInformation($"Successfully installed {game.Title} to directory {game.InstallDirectory}");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Game could not be installed");
            }
        }

        private async Task Uninstall(UninstallCommandLineOptions options)
        {
            Logger.LogInformation($"Uninstalling game with ID {options.GameId}...");

            try
            {
                var game = await GameService.GetAsync(options.GameId);

                await GameService.UninstallAsync(game);

                Logger.LogInformation($"Game successfully uninstalled from {game.InstallDirectory}");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Game could not be uninstalled");
            }
        }

        private async Task Run(RunCommandLineOptions options)
        {
            Logger.LogInformation($"Running game with ID {options.GameId}...");

            try
            {
                var game = await GameService.GetAsync(options.GameId);
                var manifest = await ManifestHelper.ReadAsync(game.InstallDirectory, game.Id);
                var action = manifest.Actions.FirstOrDefault(a => a.Id == options.ActionId);

                if (action == null)
                    action = manifest.Actions.OrderBy(a => a.SortOrder).FirstOrDefault(a => a.IsPrimaryAction);

                await GameService.Run(game, action);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Game could not run");
            }
        }

        private async Task Sync(SyncCommandLineOptions options)
        {
            Logger.LogInformation("Syncing games from server...");

            ImportService.OnImportComplete += async () =>
            {
                Logger.LogInformation("Sync complete!");
            };

            ImportService.OnImportFailed += async (Exception ex) =>
            {
                Logger.LogError(ex, "Sync failed!");
            };

            await ImportService.ImportAsync();
        }

        private async Task Import(ImportCommandLineOptions options)
        {
            if (!File.Exists(options.Path))
            {
                Logger.LogInformation("File not found! Check your path!");
                return;
            }

            switch (options.Type)
            {
                case ArchiveType.Game:
                    Logger.LogInformation("Uploading game import file to server...");

                    await Client.Games.ImportAsync(options.Path);
                    break;

                case ArchiveType.Redistributable:
                    Logger.LogInformation("Uploading redistributable archive file to server...");

                    await Client.Redistributables.ImportAsync(options.Path);
                    break;

                case ArchiveType.Server:
                    Logger.LogInformation("Uploading server archive file to server...");

                    await Client.Servers.ImportAsync(options.Path);
                    break;
            }

            Logger.LogInformation("Import complete!");
        }

        private async Task Export(ExportCommandLineOptions options)
        {
            if (File.Exists(options.Path))
            {
                Logger.LogInformation("A file at the specific path already exists!");
                return;
            }

            switch (options.Type)
            {
                case ArchiveType.Game:
                    Logger.LogInformation("Exporting game from server...");

                    await Client.Games.ExportAsync(options.Path, options.Id);
                    break;

                case ArchiveType.Redistributable:
                    Logger.LogInformation("Exporting redistributable from server...");

                    await Client.Redistributables.ExportAsync(options.Path, options.Id);
                    break;

                case ArchiveType.Server:
                    Logger.LogInformation("Exporting server from server...");

                    await Client.Servers.ExportAsync(options.Path, options.Id);
                    break;
            }

            Logger.LogInformation($"Successfully exported game to {options.Path}");
        }

        private async Task Upload(UploadCommandLineOptions options)
        {
            if (!File.Exists(options.Path))
            {
                Logger.LogInformation("File not found! Check your path!");
                return;
            }

            switch (options.Type)
            {
                case ArchiveType.Game:
                    Logger.LogInformation("Uploading game archive to server...");

                    await Client.Games.UploadArchiveAsync(options.Path, options.Id, options.Version, options.Changelog);
                    break;

                case ArchiveType.Redistributable:
                    Logger.LogInformation("Uploading redistributable archive to server...");

                    await Client.Redistributables.UploadArchiveAsync(options.Path, options.Id, options.Version, options.Changelog);
                    break;
            }
        }

        private async Task Login(LoginCommandLineOptions options)
        {
            try
            {
                if (String.IsNullOrWhiteSpace(options.ServerAddress))
                    options.ServerAddress = Settings.Authentication.ServerAddress;

                if (String.IsNullOrWhiteSpace(options.ServerAddress))
                    throw new ArgumentException("A server address must be specified");

                Client.UseServerAddress(options.ServerAddress);

                var token = await Client.AuthenticateAsync(options.Username, options.Password);

                Settings.Authentication.AccessToken = token.AccessToken;
                Settings.Authentication.RefreshToken = token.RefreshToken;
                Settings.Authentication.ServerAddress = Client.GetServerAddress();

                SettingService.SaveSettings(Settings);

                Logger.LogInformation("Logged in!");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to log in!");
            }
        }

        private async Task Logout(LogoutCommandLineOptions options)
        {
            await Client.LogoutAsync();

            Settings.Authentication.AccessToken = "";
            Settings.Authentication.RefreshToken = "";
            Settings.Authentication.OfflineMode = false;

            SettingService.SaveSettings(Settings);
        }

        private async Task ChangeAlias(ChangeAliasCommandLineOptions options)
        {
            await ProfileService.ChangeAlias(options.Alias);

            Logger.LogInformation($"Changed current user's alias from {Settings.Profile.Alias} to {options.Alias}");
        }
    }
}
