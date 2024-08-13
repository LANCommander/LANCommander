using CommandLine;
using LANCommander.Launcher.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Launcher.Services
{
    public class CommandLineService
    {
        private readonly SDK.Client Client;
        private readonly GameService GameService;
        private readonly DownloadService DownloadService;
        private readonly ImportService ImportService;
        private readonly ProfileService ProfileService;

        private Settings Settings = SettingService.GetSettings();

        public CommandLineService(SDK.Client Client, GameService GameService, ImportService ImportService, ProfileService profileService)
        {
            Client = Client;
            GameService = GameService;
            ImportService = ImportService;
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
                    LoginCommandLineOptions,
                    LogoutCommandLineOptions,
                    ChangeAliasCommandLineOptions
                >(args);

            await result.WithParsedAsync<RunScriptCommandLineOptions>(RunScript);
            await result.WithParsedAsync<InstallCommandLineOptions>(Install);
            await result.WithParsedAsync<UninstallCommandLineOptions>(Uninstall);
            await result.WithParsedAsync<SyncCommandLineOptions>(Sync);
            await result.WithParsedAsync<ImportCommandLineOptions>(Import);
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
            Console.WriteLine($"Downloading and installing game with ID {options.GameId}...");

            try
            {
                var game = await GameService.Get(options.GameId);

                await DownloadService.Add(game);
                await DownloadService.Install();

                game = await GameService.Get(options.GameId);

                Console.WriteLine($"Successfully installed {game.Title} to directory {game.InstallDirectory}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Game could not be installed: {ex.Message}");
            }
        }

        private async Task Uninstall(UninstallCommandLineOptions options)
        {
            Console.WriteLine($"Uninstalling game with ID {options.GameId}...");

            try
            {
                var game = await GameService.Get(options.GameId);

                await GameService.UninstallAsync(game);

                Console.WriteLine($"Game successfully uninstalled from {game.InstallDirectory}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Game could not be uninstalled: {ex.Message}");
            }
        }

        private async Task Sync(SyncCommandLineOptions options)
        {
            Console.WriteLine("Syncing games from server...");

            ImportService.OnImportComplete += async () =>
            {
                Console.WriteLine("Sync complete!");
            };

            ImportService.OnImportFailed += async () =>
            {
                Console.WriteLine("Sync failed!");
            };

            await ImportService.ImportAsync();
        }

        private async Task Import(ImportCommandLineOptions options)
        {
            if (!File.Exists(options.Path))
            {
                Console.WriteLine("File not found! Check your path!");
                return;
            }

            switch (options.Type)
            {
                case ImportArchiveType.Game:
                    Console.WriteLine("Uploading game import file to server...");

                    await Client.Games.ImportAsync(options.Path);
                    break;
            }

            Console.WriteLine("Import complete!");
        }

        private async Task Export(ExportCommandLineOptions options)
        {
            if (File.Exists(options.Path))
            {
                Console.WriteLine("A file at the specific path already exists!");
                return;
            }

            switch (options.Type)
            {
                case ImportArchiveType.Game:
                    Console.WriteLine("Exporting game from server...");

                    await Client.Games.ExportAsync(options.Path);
                    break;
            }

            Console.WriteLine($"Successfully exported game to {options.Path}");
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

                await Client.AuthenticateAsync(options.Username, options.Password);

                Console.WriteLine("Logged in!");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
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

            Console.WriteLine($"Changed current user's alias from {Settings.Profile.Alias} to {options.Alias}");
        }
    }
}
