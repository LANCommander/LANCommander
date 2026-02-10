using CommandLine;
using LANCommander.Launcher.Models;
using LANCommander.SDK.Helpers;
using Microsoft.Extensions.Logging;
using LANCommander.SDK;
using LANCommander.SDK.Services;

namespace LANCommander.Launcher.Services
{
    public class CommandLineService(
        ILogger<CommandLineService> logger,
        AuthenticationService authenticationService,
        UserService userService,
        GameService gameService,
        InstallService installService,
        ImportService importService,
        ProfileService profileService,
        AuthenticationClient authenticationClient,
        ScriptClient scriptClient,
        GameClient gameClient,
        RedistributableClient redistributableClient,
        ServerClient serverClient,
        IConnectionClient connectionClient,
        SettingsProvider<Settings.Settings> settingsProvider) : BaseService(logger)
    {
        public async Task ParseCommandLineAsync(string[] args)
        {
            await authenticationClient.ValidateTokenAsync();

                
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
                    await scriptClient.Game_RunInstallScriptAsync(options.InstallDirectory, options.GameId);
                    break;

                case SDK.Enums.ScriptType.Uninstall:
                    await scriptClient.Game_RunUninstallScriptAsync(options.InstallDirectory, options.GameId);
                    break;

                case SDK.Enums.ScriptType.BeforeStart:
                    await scriptClient.Game_RunBeforeStartScriptAsync(options.InstallDirectory, options.GameId);
                    break;

                case SDK.Enums.ScriptType.AfterStop:
                    await scriptClient.Game_RunAfterStopScriptAsync(options.InstallDirectory, options.GameId);
                    break;

                case SDK.Enums.ScriptType.NameChange:
                    await scriptClient.Game_RunNameChangeScriptAsync(options.InstallDirectory, options.GameId, options.NewPlayerAlias ?? Settings.Settings.DEFAULT_GAME_USERNAME);
                    break;

                case SDK.Enums.ScriptType.KeyChange:
                    await scriptClient.Game_RunKeyChangeScriptAsync(options.InstallDirectory, options.GameId, options.AllocatedKey);
                    break;
            }
        }

        private async Task Install(InstallCommandLineOptions options)
        {
            Logger.LogInformation($"Downloading and installing game with ID {options.GameId}...");

            try
            {
                var game = await gameService.GetAsync(options.GameId);

                await installService.Add(game, options.InstallDirectory);
                await installService.Next();

                game = await gameService.GetAsync(options.GameId);

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
                var game = await gameService.GetAsync(options.GameId);

                await gameService.UninstallAsync(game);

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
                /*var game = await gameService.GetAsync(options.GameId);
                var manifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Game>(game.InstallDirectory, game.Id);
                var action = manifest.Actions.FirstOrDefault(a => a.Id == options.ActionId);

                if (action == null)
                    action = manifest.Actions.OrderBy(a => a.SortOrder).FirstOrDefault(a => a.IsPrimaryAction);

                await gameService.Run(game, action);*/
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Game could not run");
            }
        }

        private async Task Sync(SyncCommandLineOptions options)
        {
            Logger.LogInformation("Syncing games from server...");

            /*importService.OnImportComplete += async () =>
            {
                Logger.LogInformation("Sync complete!");
            };

            importService.OnImportFailed += async (Exception ex) =>
            {
                Logger.LogError(ex, "Sync failed!");
            };*/

            await importService.ImportAsync();
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

                    await gameClient.ImportAsync(options.Path);
                    break;

                case ArchiveType.Redistributable:
                    Logger.LogInformation("Uploading redistributable archive file to server...");

                    await redistributableClient.ImportAsync(options.Path);
                    break;

                case ArchiveType.Server:
                    Logger.LogInformation("Uploading server archive file to server...");

                    await serverClient.ImportAsync(options.Path);
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

                    await gameClient.ExportAsync(options.Path, options.Id);
                    break;

                case ArchiveType.Redistributable:
                    Logger.LogInformation("Exporting redistributable from server...");

                    await redistributableClient.ExportAsync(options.Path, options.Id);
                    break;

                case ArchiveType.Server:
                    Logger.LogInformation("Exporting server from server...");

                    await serverClient.ExportAsync(options.Path, options.Id);
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

                    await gameClient.UploadArchiveAsync(options.Path, options.Id, options.Version, options.Changelog);
                    break;

                case ArchiveType.Redistributable:
                    Logger.LogInformation("Uploading redistributable archive to server...");

                    await redistributableClient.UploadArchiveAsync(options.Path, options.Id, options.Version, options.Changelog);
                    break;
            }
        }

        private async Task Login(LoginCommandLineOptions options)
        {
            try
            {
                if (String.IsNullOrWhiteSpace(options.ServerAddress))
                    options.ServerAddress = settingsProvider.CurrentValue.Authentication.ServerAddress.ToString();

                if (String.IsNullOrWhiteSpace(options.ServerAddress))
                    throw new ArgumentException("A server address must be specified");

                await connectionClient.UpdateServerAddressAsync(options.ServerAddress);

                var token = await authenticationClient.AuthenticateAsync(options.Username, options.Password, connectionClient.GetServerAddress());
                
                settingsProvider.Update(s =>
                {
                    s.Authentication.Token = token;
                    s.Authentication.ServerAddress = connectionClient.GetServerAddress();
                });

                Logger.LogInformation("Logged in!");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to log in!");
            }
        }

        private async Task Logout(LogoutCommandLineOptions options)
        {
            await authenticationClient.LogoutAsync();
            
            settingsProvider.Update(s =>
            {
                s.Authentication.Token = null;
                s.Authentication.OfflineModeEnabled = false;
            });
        }

        private async Task ChangeAlias(ChangeAliasCommandLineOptions options)
        {
            var currentUser = await userService.GetAsync(authenticationService.GetUserId());
            
            await profileService.ChangeAlias(options.Alias);

            Logger.LogInformation($"Changed current user's alias from {currentUser.Alias} to {options.Alias}");
        }
    }
}
